
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobotLoopDemo
{
    public partial class FormMain : Form
    {
        private CancellationTokenSource? _cts;
        private RobotLoop? _loop;

        // UI Controls
        private Button btnStart = new Button { Text = "Start", Width = 100, Left = 20, Top = 20 };
        private Button btnStop  = new Button { Text = "Stop",  Width = 100, Left = 140, Top = 20, Enabled = false };
        private Label lblState  = new Label { Text = "State: Idle", AutoSize = true, Left = 260, Top = 25 };
        private TextBox txtLog  = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Left = 20, Top = 60, Width = 740, Height = 360 };
        private CheckBox chkAutoLoop = new CheckBox { Text = "Auto loop liên tục", Checked = true, Left = 620, Top = 25 };

        public FormMain()
        {
            InitializeComponent();
            Text = "Robot Loop Template (PC ↔ Robot)";
            Width = 800; Height = 480;
            Controls.AddRange(new Control[] { btnStart, btnStop, lblState, txtLog, chkAutoLoop });

            btnStart.Click += async (s, e) => await StartLoopAsync();
            btnStop.Click  += (s, e) => StopLoop();
        }

        private async Task StartLoopAsync()
        {
            btnStart.Enabled = false;
            btnStop.Enabled  = true;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Start loop...\r\n");

            _cts = new CancellationTokenSource();

            // Khởi tạo loop với IP/Port robot và callback cập nhật UI
            _loop = new RobotLoop(
                ip: "192.168.0.10",      // TODO: thay IP robot
                port: 30002,             // TODO: thay port robot
                autoLoop: chkAutoLoop.Checked,
                onStateChanged: state => BeginInvoke(new Action(() =>
                {
                    lblState.Text = $"State: {state}";
                })),
                onLog: line => BeginInvoke(new Action(() =>
                {
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}\r\n");
                })),
                onFault: err => BeginInvoke(new Action(() =>
                {
                    txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {err}\r\n");
                }))
            );

            try
            {
                await _loop.RunAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // bình thường khi Stop
            }
            catch (Exception ex)
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Loop crashed: {ex.Message}\r\n");
            }
            finally
            {
                btnStart.Enabled = true;
                btnStop.Enabled  = false;
            }
        }

        private void StopLoop()
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Stop requested.\r\n");
            btnStop.Enabled = false;
            _cts?.Cancel();
        }
    }

    // ===== TCP client gửi/nhận lệnh dạng chuỗi =====
    public class RobotStringClient : IDisposable
    {
        private readonly string ip;
        private readonly int port;
        private TcpClient? client;
        private NetworkStream? stream;

        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan SendTimeout    { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(3);

        public RobotStringClient(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public async Task ConnectAsync(CancellationToken ct)
        {
            client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ConnectTimeout);
            await client.ConnectAsync(ip, port, cts.Token);
            stream = client.GetStream();
            stream.WriteTimeout = (int)SendTimeout.TotalMilliseconds;
            stream.ReadTimeout  = (int)ReceiveTimeout.TotalMilliseconds;
        }

        public async Task<string> SendCommandAsync(string command, CancellationToken ct)
        {
            if (stream == null) throw new InvalidOperationException("Not connected.");
            string framed = EnsureLineTerminator(command);
            byte[] tx = Encoding.ASCII.GetBytes(framed);
            await stream.WriteAsync(tx, 0, tx.Length, ct);
            await stream.FlushAsync(ct);

            // đọc đến khi gặp \n hoặc hết timeout/hủy
            var buffer = new byte[4096];
            var sb = new StringBuilder();
            while (true)
            {
                int read = await ReadWithTimeoutAsync(stream, buffer, ct);
                if (read <= 0) break;
                sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
                if (sb.ToString().Contains("\n")) break;
            }
            return sb.ToString().TrimEnd('\r', '\n');
        }

        public async Task<string> SendWithRetryAsync(string command, int maxRetry, CancellationToken ct)
        {
            Exception? lastEx = null;
            for (int attempt = 1; attempt <= maxRetry + 1; attempt++)
            {
                try
                {
                    var reply = await SendCommandAsync(command, ct);
                    if (IsAck(reply)) return reply;
                    if (IsError(reply)) throw new InvalidOperationException($"Robot error: {reply}");
                    return reply; // nếu giao thức không có ACK rõ ràng
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    await Task.Delay(150, ct);
                }
            }
            throw new InvalidOperationException($"Failed after retries. Last: {lastEx?.Message}", lastEx);
        }

        private static string EnsureLineTerminator(string cmd)
        {
            if (cmd.EndsWith("\n") || cmd.EndsWith("\r\n")) return cmd;
            return cmd + "\n";
        }
        private static bool IsAck(string reply)
        {
            return reply.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
                || reply.StartsWith("ACK", StringComparison.OrdinalIgnoreCase);
        }
        private static bool IsError(string reply)
        {
            return reply.StartsWith("ERR", StringComparison.OrdinalIgnoreCase)
                || reply.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase);
        }
        private static async Task<int> ReadWithTimeoutAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
        {
            try
            {
                return await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        public void Dispose()
        {
            try { stream?.Dispose(); } catch { }
            try { client?.Close(); } catch { }
            stream = null; client = null;
        }
    }

    // ===== State machine điều phối chu trình =====
    public class RobotLoop
    {
        public enum LoopState
        {
            Idle,
            Connecting,
            Handshake,
            WaitingReady,
            LoadRecipe,
            StartCycle,
            MonitorCycle,
            FinishCycle,
            Reset,
            BackToIdle,
            Fault
        }

        private readonly string ip;
        private readonly int port;
        private readonly bool autoLoop;
        private readonly Action<string> onLog;
        private readonly Action<string> onFault;
        private readonly Action<LoopState> onStateChanged;

        private RobotStringClient? client;
        private LoopState state = LoopState.Idle;

        // Thông số ví dụ (bạn chỉnh theo nhu cầu)
        private int recipeId = 1;
        private TimeSpan pollInterval = TimeSpan.FromMilliseconds(150);
        private TimeSpan cycleTimeout = TimeSpan.FromSeconds(30);

        public RobotLoop(
            string ip,
            int port,
            bool autoLoop,
            Action<LoopState> onStateChanged,
            Action<string> onLog,
            Action<string> onFault)
        {
            this.ip = ip;
            this.port = port;
            this.autoLoop = autoLoop;
            this.onStateChanged = onStateChanged;
            this.onLog = onLog;
            this.onFault = onFault;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            state = LoopState.Idle;
            EmitState();

            client = new RobotStringClient(ip, port);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    switch (state)
                    {
                        case LoopState.Idle:
                            // vào Idle xong thì chuyển sang Connecting để chuẩn bị vòng lặp
                            await Task.Delay(200, ct);
                            state = LoopState.Connecting; EmitState();
                            break;

                        case LoopState.Connecting:
                            onLog("Connecting to robot...");
                            await client.ConnectAsync(ct);
                            onLog("Connected.");
                            state = LoopState.Handshake; EmitState();
                            break;

                        case LoopState.Handshake:
                            // Kiểm tra robot có sẵn sàng không (ví dụ lệnh PING/STATE)
                            var ping = await client.SendWithRetryAsync("GET STATE", 1, ct);
                            onLog($"STATE reply: {ping}");
                            // Giả định nếu reply chứa READY thì OK, nếu không ta vẫn chuyển WaitingReady
                            state = LoopState.WaitingReady; EmitState();
                            break;

                        case LoopState.WaitingReady:
                            onLog("Waiting robot READY...");
                            while (!ct.IsCancellationRequested)
                            {
                                var readyReply = await client.SendWithRetryAsync("GET READY", 1, ct);
                                bool ready = ParseBool(readyReply); // ví dụ: "READY=1"
                                if (ready) break;
                                await Task.Delay(pollInterval, ct);
                            }
                            onLog("Robot READY.");
                            state = LoopState.LoadRecipe; EmitState();
                            break;

                        case LoopState.LoadRecipe:
                            onLog($"Load recipe {recipeId}...");
                            var setRecipe = await client.SendWithRetryAsync($"SET RECIPE={recipeId}", 2, ct);
                            onLog($"Recipe set: {setRecipe}");
                            state = LoopState.StartCycle; EmitState();
                            break;

                        case LoopState.StartCycle:
                            onLog("Start cycle...");
                            var start = await client.SendWithRetryAsync("START MAIN", 2, ct);
                            onLog($"Start reply: {start}");
                            state = LoopState.MonitorCycle; EmitState();
                            break;

                        case LoopState.MonitorCycle:
                            onLog("Monitor cycle running...");
                            var sw = Stopwatch.StartNew();
                            while (!ct.IsCancellationRequested)
                            {
                                // Theo dõi hoàn thành chu trình
                                var doneReply = await client.SendWithRetryAsync("GET DONE", 1, ct);
                                bool done = ParseBool(doneReply); // ví dụ: "DONE=1"
                                if (done)
                                {
                                    onLog($"Cycle DONE in {sw.Elapsed.TotalSeconds:F2}s");
                                    break;
                                }

                                // Tùy chọn: cũng đọc lỗi/trạng thái
                                var faultReply = await client.SendWithRetryAsync("GET FAULT", 1, ct);
                                bool fault = ParseBool(faultReply);
                                if (fault)
                                {
                                    state = LoopState.Fault; EmitState();
                                    throw new InvalidOperationException("Robot fault detected.");
                                }

                                if (sw.Elapsed > cycleTimeout)
                                {
                                    state = LoopState.Fault; EmitState();
                                    throw new TimeoutException("Cycle timeout.");
                                }

                                await Task.Delay(pollInterval, ct);
                            }
                            state = LoopState.FinishCycle; EmitState();
                            break;

                        case LoopState.FinishCycle:
                            // Ví dụ: lấy dữ liệu kết quả, clear flag
                            var data = await client.SendWithRetryAsync("GET R10", 1, ct);
                            onLog($"Result data: {data}");
                            // Gửi acknowledge cho robot (nếu cần)
                            await client.SendWithRetryAsync("SET ACK=1", 1, ct);
                            state = LoopState.Reset; EmitState();
                            break;

                        case LoopState.Reset:
                            // Clear handshake/ack để quay về trạng thái ban đầu
                            await client.SendWithRetryAsync("SET ACK=0", 1, ct);
                            await client.SendWithRetryAsync("SET DONE=0", 1, ct);
                            onLog("Reset flags.");
                            state = LoopState.BackToIdle; EmitState();
                            break;

                        case LoopState.BackToIdle:
                            onLog("Back to Idle/Ready for next cycle.");
                            // Nếu autoLoop thì về WaitingReady để chạy vòng tiếp theo,
                            // nếu không thì ở Idle (chờ người dùng Start lại).
                            if (autoLoop)
                                state = LoopState.WaitingReady;
                            else
                                state = LoopState.Idle;
                            EmitState();
                            break;

                        case LoopState.Fault:
                            onFault("Fault state. Waiting 2s then reconnect.");
                            await Task.Delay(2000, ct);
                            // Thử reconnect lại từ đầu
                            state = LoopState.Connecting; EmitState();
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Stop được yêu cầu
                    break;
                }
                catch (Exception ex)
                {
                    onFault($"Exception: {ex.Message}");
                    // Về Fault và thử lại sau 2s
                    state = LoopState.Fault; EmitState();
                }
            }

            client?.Dispose();
            onLog("Loop stopped.");
            state = LoopState.Idle; EmitState();
        }

        private void EmitState() => onStateChanged(state);

        private static bool ParseBool(string reply)
        {
            // ví dụ các format phổ biến:
            // "READY=1", "DONE=0", "OK", "ACK", "TRUE", "FALSE"
            if (reply.IndexOf("=1", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (reply.IndexOf("=0", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (reply.Equals("OK", StringComparison.OrdinalIgnoreCase)) return true;
            if (reply.Equals("ACK", StringComparison.OrdinalIgnoreCase)) return true;
            if (reply.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
            if (reply.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
            // fallback: coi khác rỗng là true (tuỳ chỉnh thêm)
            return !string.IsNullOrWhiteSpace(reply);
        }
    }
}
``
