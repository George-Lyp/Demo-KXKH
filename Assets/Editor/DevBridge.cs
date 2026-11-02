// ---------------------------------------------------------------
// DevBridge —— 让外部工具（AI / curl / 脚本）读取 Unity 编辑器状态的本地 HTTP 桥
//
// 启动：菜单 Tools > DevBridge > Start      （日志会打印监听地址）
// 停止：菜单 Tools > DevBridge > Stop
// 端口：127.0.0.1:7777（只监听本机，外部机器连不进来）
//
// 接口：
//   GET  /ping        版本 + 是否在运行
//   GET  /dump        当前场景的完整快照（Hierarchy + 每个组件的字段值 + 运行时值）
//   GET  /log?n=40    Editor.log 末尾 n 行（= Console 里的内容）
//   GET  /play        进入 Play 模式
//   GET  /stop        退出 Play 模式
//   GET  /pause       暂停 / 继续
//
// 示例：curl.exe --noproxy "*" http://127.0.0.1:7777/dump
//
// 说明：只在编辑器里编译（Assets/Editor 约定），不会进打包。
//      /dump 用 SerializedObject 读取 inspector 上的真实值，
//      并对 Rigidbody2D / Animator 额外补运行时值（velocity、动画参数等），
//      这些在 inspector 上看不到。
// ---------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DevBridge
{
    // 0 = 让操作系统分配一个空闲端口。
    // 固定端口会被「上一次没退干净的 Unity 进程」永久占住（AddressAlreadyInUse），
    // 而系统分配的端口永远不会冲突。实际端口会写到 Library/DevBridge.port 供外部读取。
    const int Port = 0;
    static int actualPort;
    const string PrefKey = "DevBridge.Enabled";

    const string BridgeVersion = "2026-11-02c";

    static TcpListener listener;
    static Thread thread;
    static volatile bool running;
    static volatile string lastError = "";
    static volatile bool editorBusy;   // 主线程在 Pump() 里写入，后台线程只读
    static readonly ConcurrentQueue<Pending> queue = new ConcurrentQueue<Pending>();

    class Pending
    {
        public string Method;
        public string Path;
        public string Query;
        public string Response = "";
        public string ContentType = "application/json";
        public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
    }

    static DevBridge()
    {
        EditorApplication.update += Pump;
        // 关键：域重载前必须把 socket 关掉。
        // 否则每次脚本重编译都会留下一个没人 accept 的监听，端口被占死。
        AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        EditorApplication.quitting += Shutdown;
        if (EditorPrefs.GetBool(PrefKey, false))
            EditorApplication.delayCall += Start;
    }

    // ------------------------------------------------ 生命周期

    [MenuItem("Tools/DevBridge/Start", false, 1)]
    static void Start()
    {
        if (running && listener != null) { Debug.Log("[DevBridge] 已在运行：http://127.0.0.1:" + actualPort); return; }

        Shutdown();   // 先把上一次的 socket 关干净，避免同一端口被重复绑定
        try
        {
            listener = new TcpListener(IPAddress.Loopback, Port);
            // 绝对不要设置 SocketOptionName.ReuseAddress。
            // Windows 下它允许两个 socket 同时绑同一端口；后来的那个会把新连接抢走却没人 accept，
            // 表现就是 netstat 里 LISTENING 但 curl 永远超时（连接停在 CLOSE_WAIT）。
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
            listener.Start(16);
            actualPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            running = true;
            EditorPrefs.SetBool(PrefKey, true);
            WritePortFile(actualPort);

            thread = new Thread(Loop) { IsBackground = true, Name = "DevBridge" };
            thread.Start();
            Debug.Log("[DevBridge] 已启动 http://127.0.0.1:" + actualPort +
                "  端口已写入 Library/DevBridge.port  （curl.exe --noproxy \"*\" http://127.0.0.1:" + actualPort + "/ping）");
        }
        catch (SocketException se)
        {
            running = false;
            lastError = se.SocketErrorCode + ": " + se.Message;
            try { if (listener != null) listener.Stop(); } catch { }
            listener = null;
            Debug.LogError("[DevBridge] 绑定端口失败（" + se.SocketErrorCode + "）：" + se.Message +
                "  当前用系统分配端口（Port=0），正常不会冲突；若仍失败，检查是否有第二个 Unity 实例在跑同一个工程。");
        }
        catch (Exception e)
        {
            running = false;
            lastError = e.GetType().Name + ": " + e.Message;
            try { if (listener != null) listener.Stop(); } catch { }
            listener = null;
            Debug.LogError("[DevBridge] 启动失败：" + e.Message);
        }
    }

    [MenuItem("Tools/DevBridge/Stop", false, 2)]
    static void Stop()
    {
        Shutdown();
        EditorPrefs.SetBool(PrefKey, false);
        Debug.Log("[DevBridge] 已停止");
    }

    // 只关 socket，不动 EditorPrefs：域重载 / 退出编辑器时用这个，否则下次不会自动启动
    static void Shutdown()
    {
        running = false;

        try { if (listener != null) listener.Stop(); } catch { }
        listener = null;

        Thread t = thread;
        thread = null;
        if (t != null) { try { t.Join(300); } catch { } }

        // 把还在等主线程的请求统统放行，避免它们干等到超时
        Pending p;
        while (queue.TryDequeue(out p)) { try { p.Done.Set(); } catch { } }

        DeletePortFile();
    }

    // ------------------------------------------------ 端口文件

    static string PortFilePath
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "DevBridge.port")); }
    }

    static void WritePortFile(int port)
    {
        try
        {
            string p = PortFilePath;
            string dir = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(p, port.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception e) { Debug.LogWarning("[DevBridge] 写端口文件失败：" + e.Message); }
    }

    static void DeletePortFile()
    {
        try { string p = PortFilePath; if (File.Exists(p)) File.Delete(p); } catch { }
    }

    static void Loop()
    {
        while (running)
        {
            TcpClient client;
            try { client = listener.AcceptTcpClient(); }
            catch (ObjectDisposedException) { break; }                    // Shutdown() 关掉了 listener
            catch (SocketException) { if (!running) break; Thread.Sleep(50); continue; }
            catch (Exception) { break; }

            if (!running) { try { client.Close(); } catch { } break; }

            try { Handle(client); }
            catch (Exception e) { try { client.Close(); } catch { } Debug.LogWarning("[DevBridge] " + e.Message); }
        }
        running = false;
    }

    static void Handle(TcpClient client)
    {
        using (client)
        using (NetworkStream ns = client.GetStream())
        {
            string head = ReadHead(ns);
            if (string.IsNullOrEmpty(head)) return;

            string[] lines = head.Split('\n');
            string[] first = lines[0].Trim().Split(' ');
            if (first.Length < 2) return;

            string path = first[1];
            string query = "";
            int q = path.IndexOf('?');
            if (q >= 0) { query = path.Substring(q + 1); path = path.Substring(0, q); }

            Pending p = new Pending { Method = first[0], Path = path, Query = query };
            queue.Enqueue(p);

            // 这里绝对不能直接读 EditorApplication.isCompiling / isUpdating：
            // 它们是主线程专属 API，后台线程一调就抛 "can only be called from the main thread"，
            // 异常被 Loop 的 catch 吞掉 -> 连接被直接关闭 -> curl 收到空响应（exit 52）。
            // 主线程已经在 Pump() 里把状态写进 editorBusy，这里读标志即可。
            bool busy = editorBusy;
            int waitMs = busy ? 1500 : 8000;

            if (!p.Done.Wait(waitMs))
            {
                p.Response = "{\"ok\":false,\"busy\":" + B(busy) + ",\"error\":\"主线程 " + (waitMs / 1000) +
                             " 秒内没响应（编辑器卡住 / 正在编译 / 正在导入）\"}";
            }
            WriteResponse(ns, p.Response, p.ContentType);
        }
    }

    static string ReadHead(NetworkStream ns)
    {
        StringBuilder sb = new StringBuilder();
        byte[] buf = new byte[1];
        int guard = 0;
        try
        {
            while (ns.Read(buf, 0, 1) > 0)
            {
                sb.Append((char)buf[0]);
                if (++guard > 8192) break;
                int L = sb.Length;
                if (L >= 4 && sb[L - 1] == '\n' && sb[L - 2] == '\r' && sb[L - 3] == '\n' && sb[L - 4] == '\r')
                    break;
            }
        }
        catch { }
        return sb.ToString();
    }

    static void WriteResponse(NetworkStream ns, string body, string type)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body ?? "");
        string head =
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: " + type + "; charset=utf-8\r\n" +
            "Content-Length: " + bytes.Length + "\r\n" +
            "Connection: close\r\n\r\n";
        byte[] hb = Encoding.ASCII.GetBytes(head);
        ns.Write(hb, 0, hb.Length);
        ns.Write(bytes, 0, bytes.Length);
        ns.Flush();
    }

    // ------------------------------------------------ 主线程派发

    static void Pump()
    {
        // EditorApplication.isCompiling / isUpdating 只能在主线程读，所以在这里存一份给后台线程用
        editorBusy = EditorApplication.isCompiling || EditorApplication.isUpdating;

        Pending p;
        while (queue.TryDequeue(out p))
        {
            if (p == null) continue;
            try { Dispatch(p); }
            catch (Exception e)
            {
                p.Response = "{\"ok\":false,\"error\":" + J(e.GetType().Name + ": " + e.Message) + "}";
            }
            p.Done.Set();
        }
    }

    static void Dispatch(Pending p)
    {
        string path = (p.Path ?? "/").TrimEnd('/');
        if (path.Length == 0) path = "/";

        switch (path)
        {
            case "/":
            case "/help":
                p.Response = "{\"ok\":true,\"endpoints\":[\"/ping\",\"/version\",\"/dump\",\"/log?n=40\",\"/play\",\"/stop\",\"/pause\",\"/restart\"]}";
                return;

            case "/ping":
                p.Response = "{\"ok\":true,\"unity\":" + J(Application.unityVersion) +
                             ",\"bridge\":\"" + BridgeVersion + "\"" +
                             ",\"port\":" + actualPort +
                             ",\"running\":" + B(running) +
                             ",\"lastError\":" + J(lastError ?? "") +
                             ",\"playing\":" + B(EditorApplication.isPlaying) +
                             ",\"paused\":" + B(EditorApplication.isPaused) +
                             ",\"compiling\":" + B(EditorApplication.isCompiling) +
                             ",\"updating\":" + B(EditorApplication.isUpdating) +
                             ",\"scene\":" + J(SceneManager.GetActiveScene().name) + "}";
                return;

            case "/version":
                p.Response = "{\"ok\":true,\"bridge\":\"" + BridgeVersion + "\",\"port\":" + actualPort +
                             ",\"running\":" + B(running) + ",\"lastError\":" + J(lastError ?? "") + "}";
                return;

            case "/restart":
                p.Response = "{\"ok\":true,\"note\":\"shutdown+start 已排队，约半秒后生效\"}";
                EditorApplication.delayCall += () => { Shutdown(); Start(); };
                return;

            case "/dump":
                p.Response = Dump();
                return;

            case "/log":
                p.Response = LogTail(ParseInt(p.Query, "n", 40));
                p.ContentType = "text/plain";
                return;

            case "/play":
                EditorApplication.isPlaying = true;
                p.Response = "{\"ok\":true,\"playing\":true}";
                return;

            case "/stop":
                EditorApplication.isPlaying = false;
                p.Response = "{\"ok\":true,\"playing\":false}";
                return;

            case "/pause":
                EditorApplication.isPaused = !EditorApplication.isPaused;
                p.Response = "{\"ok\":true,\"paused\":" + B(EditorApplication.isPaused) + "}";
                return;

            default:
                p.Response = "{\"ok\":false,\"error\":\"未知接口 " + J(path) + "\"}";
                return;
        }
    }

    // ------------------------------------------------ /dump

    static string Dump()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        StringBuilder sb = new StringBuilder(8192);
        sb.Append("{\"ok\":true");
        sb.Append(",\"isPlaying\":").Append(B(EditorApplication.isPlaying));
        sb.Append(",\"isPaused\":").Append(B(EditorApplication.isPaused));
        sb.Append(",\"scene\":").Append(J(scene.name));
        sb.Append(",\"scenePath\":").Append(J(scene.path));
        sb.Append(",\"frame\":").Append(Time.frameCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"time\":").Append(F(Time.time));
        sb.Append(",\"objects\":[");

        bool firstObj = true;
        int emitted = 0;
        for (int i = 0; i < roots.Length && emitted < 200; i++)
            Walk(roots[i], "", sb, ref firstObj, ref emitted);

        sb.Append("]}");
        return sb.ToString();
    }

    static void Walk(GameObject go, string parentPath, StringBuilder sb, ref bool firstObj, ref int emitted)
    {
        if (emitted >= 200) return;
        emitted++;

        string path = parentPath + "/" + go.name;

        if (!firstObj) sb.Append(',');
        firstObj = false;

        sb.Append("{\"name\":").Append(J(go.name));
        sb.Append(",\"path\":").Append(J(path));
        sb.Append(",\"activeSelf\":").Append(B(go.activeSelf));
        sb.Append(",\"activeInHierarchy\":").Append(B(go.activeInHierarchy));
        sb.Append(",\"tag\":").Append(J(go.tag));
        sb.Append(",\"layer\":").Append(go.layer.ToString(CultureInfo.InvariantCulture));
        sb.Append(",\"layerName\":").Append(J(LayerMask.LayerToName(go.layer)));

        Transform tr = go.transform;
        Vector3 pos = tr.position;
        Vector3 lpos = tr.localPosition;
        Vector3 scale = tr.localScale;
        sb.Append(",\"pos\":[").Append(F(pos.x)).Append(',').Append(F(pos.y)).Append(',').Append(F(pos.z)).Append(']');
        sb.Append(",\"localPos\":[").Append(F(lpos.x)).Append(',').Append(F(lpos.y)).Append(',').Append(F(lpos.z)).Append(']');
        sb.Append(",\"scale\":[").Append(F(scale.x)).Append(',').Append(F(scale.y)).Append(',').Append(F(scale.z)).Append(']');
        sb.Append(",\"rotationZ\":").Append(F(tr.eulerAngles.z));

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sb.Append(",\"sprite\":{");
            sb.Append("\"name\":").Append(J(sr.sprite != null ? sr.sprite.name : "null"));
            sb.Append(",\"sortingLayer\":").Append(J(sr.sortingLayerName));
            sb.Append(",\"order\":").Append(sr.sortingOrder.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"color\":").Append(J(ColorUtility.ToHtmlStringRGBA(sr.color)));
            sb.Append(",\"flipX\":").Append(B(sr.flipX));
            sb.Append(",\"enabled\":").Append(B(sr.enabled));
            sb.Append('}');
        }

        sb.Append(",\"components\":[");
        Component[] comps = go.GetComponents<Component>();
        bool firstComp = true;
        int compCount = 0;
        for (int i = 0; i < comps.Length && compCount < 16; i++)
        {
            Component c = comps[i];
            if (c == null)
            {
                if (!firstComp) sb.Append(',');
                firstComp = false; compCount++;
                sb.Append("{\"type\":\"<MissingScript>\"}");
                continue;
            }
            if (!firstComp) sb.Append(',');
            firstComp = false;
            compCount++;

            sb.Append("{\"type\":").Append(J(c.GetType().Name));
            sb.Append(",\"enabled\":").Append(B(BehaviourEnabled(c)));

            sb.Append(",\"fields\":{");
            try { WriteSerializedFields(c, sb); }
            catch (Exception e) { if (sb.Length > 0 && sb[sb.Length - 1] == ',') sb.Length--; sb.Append("\"_err\":").Append(J(e.Message)); }
            sb.Append('}');

            string extra = null;
            try { extra = ExtraRuntime(c); }
            catch (Exception e2) { extra = "{\"_err\":" + J(e2.GetType().Name) + "}"; }
            if (extra != null) sb.Append(",\"runtime\":").Append(extra);

            sb.Append('}');
        }
        sb.Append(']');

        sb.Append('}');

        for (int i = 0; i < go.transform.childCount; i++)
            Walk(go.transform.GetChild(i).gameObject, path, sb, ref firstObj, ref emitted);
    }

    static bool BehaviourEnabled(Component c)
    {
        Behaviour b = c as Behaviour;
        if (b != null) return b.enabled;
        Collider2D co = c as Collider2D;
        if (co != null) return co.enabled;
        Renderer r = c as Renderer;
        if (r != null) return r.enabled;
        return true;
    }

    static void WriteSerializedFields(Component c, StringBuilder sb)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty prop = so.GetIterator();
        bool enter = true;
        bool firstField = true;
        int guard = 0;

        while (prop.NextVisible(enter) && guard++ < 80)
        {
            enter = false;
            if (prop.depth != 0) continue;
            if (prop.propertyType == SerializedPropertyType.ArraySize) continue;

            string v = PropValue(prop);
            if (v == null) continue;

            if (!firstField) sb.Append(',');
            firstField = false;
            sb.Append(J(prop.name)).Append(':').Append(v);
        }
    }

    static string PropValue(SerializedProperty p)
    {
        try { return PropValueCore(p); }
        catch (Exception ex) { return J("<unreadable:" + ex.GetType().Name + ">"); }
    }

    static string PropValueCore(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Float: return F(p.floatValue);
            case SerializedPropertyType.Integer: return p.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean: return B(p.boolValue);
            case SerializedPropertyType.String: return J(p.stringValue);
            case SerializedPropertyType.Character: return J(p.stringValue);
            case SerializedPropertyType.Enum:
                string[] names = p.enumNames;
                return (names != null && p.enumValueIndex >= 0 && p.enumValueIndex < names.Length)
                    ? J(names[p.enumValueIndex] + "(" + p.enumValueIndex + ")")
                    : p.enumValueIndex.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.ObjectReference:
                UnityEngine.Object o = p.objectReferenceValue;
                return o == null ? "null" : J(o.name + " [" + o.GetType().Name + "]");
            case SerializedPropertyType.Vector2:
                return "[" + F(p.vector2Value.x) + "," + F(p.vector2Value.y) + "]";
            case SerializedPropertyType.Vector3:
                return "[" + F(p.vector3Value.x) + "," + F(p.vector3Value.y) + "," + F(p.vector3Value.z) + "]";
            case SerializedPropertyType.Vector2Int:
                return "[" + p.vector2IntValue.x + "," + p.vector2IntValue.y + "]";
            case SerializedPropertyType.Vector3Int:
                return "[" + p.vector3IntValue.x + "," + p.vector3IntValue.y + "," + p.vector3IntValue.z + "]";
            case SerializedPropertyType.Color:
                return J(ColorUtility.ToHtmlStringRGBA(p.colorValue));
            case SerializedPropertyType.LayerMask:
                int lm = p.intValue;
                string lmName = (lm >= 0 && lm <= 31) ? LayerMask.LayerToName(lm) : "(all)";
                return J(lm.ToString(CultureInfo.InvariantCulture) + " (" + lmName + ")");
            case SerializedPropertyType.Rect:
                return "[" + F(p.rectValue.x) + "," + F(p.rectValue.y) + "," + F(p.rectValue.width) + "," + F(p.rectValue.height) + "]";
            default:
                return null;
        }
    }

    static string ExtraRuntime(Component c)
    {
        Rigidbody2D rb = c as Rigidbody2D;
        if (rb != null)
        {
            return "{\"velocity\":[" + F(rb.velocity.x) + "," + F(rb.velocity.y) + "]" +
                   ",\"speed\":" + F(rb.velocity.magnitude) +
                   ",\"bodyType\":{\"value\":" + J(rb.bodyType.ToString()) + "}" +
                   ",\"looksStill\":" + B(Mathf.Abs(rb.velocity.y) < 0.01f) + "}";
        }

        Animator an = c as Animator;
        if (an != null)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\"parameters\":{");
            bool first = true;
            AnimatorControllerParameter[] ps = an.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                AnimatorControllerParameter prm = ps[i];
                if (!first) sb.Append(',');
                first = false;
                sb.Append(J(prm.name)).Append(':');
                switch (prm.type)
                {
                    case AnimatorControllerParameterType.Float: sb.Append(F(an.GetFloat(prm.name))); break;
                    case AnimatorControllerParameterType.Int: sb.Append(an.GetInteger(prm.name).ToString(CultureInfo.InvariantCulture)); break;
                    default: sb.Append(B(an.GetBool(prm.name))); break;
                }
            }
            sb.Append('}');

            AnimatorStateInfo st = an.GetCurrentAnimatorStateInfo(0);
            int hash = st.shortNameHash;
            string stateName = "(hash " + hash + ")";
            if (an.runtimeAnimatorController != null)
            {
                AnimationClip[] clips = an.runtimeAnimatorController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i] != null && Animator.StringToHash(clips[i].name) == hash)
                    { stateName = clips[i].name; break; }
                }
            }
            sb.Append(",\"currentState\":").Append(J(stateName));
            sb.Append(",\"normalizedTime\":").Append(F(st.normalizedTime));
            sb.Append(",\"speed\":").Append(F(an.speed));
            sb.Append('}');
            return sb.ToString();
        }

        Collider2D col = c as Collider2D;
        if (col != null)
        {
            Collider2D[] hits = new Collider2D[8];
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            int n = col.OverlapCollider(filter, hits);
            return "{\"overlapping\":" + n + "}";
        }

        MonoBehaviour mb = c as MonoBehaviour;
        if (mb != null)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("{\"props\":{");
            bool first = true;
            int guard = 0;
            PropertyInfo[] pis = mb.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < pis.Length && guard < 40; i++)
            {
                PropertyInfo pi = pis[i];
                if (!pi.CanRead || pi.GetIndexParameters().Length > 0) continue;
                Type rt = pi.PropertyType;
                if (rt != typeof(float) && rt != typeof(int) && rt != typeof(bool) && rt != typeof(string) && rt != typeof(Vector2)) continue;
                object v;
                try { v = pi.GetValue(mb, null); }
                catch { continue; }
                if (v == null) continue;
                guard++;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(J(pi.Name)).Append(':');
                if (rt == typeof(float)) sb.Append(F((float)v));
                else if (rt == typeof(int)) sb.Append(((int)v).ToString(CultureInfo.InvariantCulture));
                else if (rt == typeof(bool)) sb.Append(B((bool)v));
                else if (rt == typeof(Vector2)) { Vector2 vv = (Vector2)v; sb.Append("[").Append(F(vv.x)).Append(',').Append(F(vv.y)).Append(']'); }
                else sb.Append(J((string)v));
            }
            sb.Append("}}");
            return sb.ToString();
        }

        return null;
    }

    // ------------------------------------------------ /log

    static string LogTail(int n)
    {
        if (n < 1) n = 1;
        if (n > 500) n = 500;
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Unity", "Editor");
        string file = Path.Combine(dir, "Editor.log");
        if (!File.Exists(file)) return "(Editor.log 不存在：" + file + ")";

        try
        {
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long len = fs.Length;
                int take = (int)Math.Min(len, 400000);
                fs.Seek(len - take, SeekOrigin.Begin);
                byte[] buf = new byte[take];
                fs.Read(buf, 0, take);
                string all = Encoding.UTF8.GetString(buf).Replace("\0", "");
                string[] lines = all.Split('\n');
                int start = Math.Max(0, lines.Length - n);
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < lines.Length; i++) sb.Append(lines[i].TrimEnd('\r')).Append('\n');
                return sb.ToString();
            }
        }
        catch (Exception e) { return "(读取失败：" + e.Message + ")"; }
    }

    // ------------------------------------------------ 小工具

    static int ParseInt(string query, string key, int fallback)
    {
        if (string.IsNullOrEmpty(query)) return fallback;
        string[] parts = query.Split('&');
        for (int i = 0; i < parts.Length; i++)
        {
            string[] kv = parts[i].Split('=');
            if (kv.Length == 2 && kv[0] == key)
            {
                int v;
                if (int.TryParse(kv[1], out v)) return v;
            }
        }
        return fallback;
    }

    static string F(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return "null";
        return v.ToString("0.####", CultureInfo.InvariantCulture);
    }

    static string B(bool v) { return v ? "true" : "false"; }

    static string J(string s)
    {
        if (s == null) return "null";
        StringBuilder sb = new StringBuilder(s.Length + 8);
        sb.Append('"');
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
