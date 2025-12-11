
using System;
using System.Net.Sockets;
using NModbus;          // NuGet: NModbus4
using NModbus.Logging;

class Program
{
    static void Main()
    {
        string plcIp = "192.168.1.10";
        int port = 502;
        byte unitId = 1;               // Slave ID (Station No.). Thường là 1 trên DVP-SE.

        using (var client = new TcpClient())
        {
            client.ReceiveTimeout = 2000;
            client.SendTimeout = 2000;
            client.Connect(plcIp, port);

            var factory = new ModbusFactory(new SilentModbusLogger());
            var master = factory.CreateMaster(client.GetStream());

            // ==== Ví dụ 1: Đọc 10 thanh ghi D từ D0 (HR addr 4096) ====
            ushort startHrAddr = 4096;  // D0
            ushort numPoints    = 10;   // D0..D9
            ushort[] regs = master.ReadHoldingRegisters(unitId, startHrAddr, numPoints);

            Console.WriteLine("Read D0..D9:");
            for (int i = 0; i < regs.Length; i++)
                Console.WriteLine($"D{i}: {regs[i]}");

            // ==== Ví dụ 2: Ghi 1 thanh ghi: ghi 1234 vào D0 ====
            master.WriteSingleRegister(unitId, 4096, 1234);

            // ==== Ví dụ 3: Ghi nhiều thanh ghi liên tiếp: D10..D12 ====
            ushort[] values = new ushort[] { 100, 200, 300 };
            master.WriteMultipleRegisters(unitId, 4106, values); // D10 ↔ 4106

            // ==== Ví dụ 4: Đọc trạng thái X0 (Discrete Input addr 1024) ====
            // Modbus ReadDiscreteInputs trả bool[]
            bool[] xInputs = master.ReadInputs(unitId, 1024, 8); // X0..X7
            Console.WriteLine($"X0: {xInputs[0]}, X1: {xInputs[1]}");

            // ==== Ví dụ 5: Đọc/ghi M0 (coil) ====
            bool[] mCoils = master.ReadCoils(unitId, 2048, 8);   // M0..M7
            Console.WriteLine($"M0: {mCoils[0]}");

            // Bật M0:
            master.WriteSingleCoil(unitId, 2048, true);
        }
    }
}

//Modbus trả về ushort (16‑bit). Nếu bạn cần Int32/Float (ghép 2 thanh ghi), hãy chú ý word order (nhiều hệ thống dùng “Big‑Endian / Word‑High trước”). Ví dụ ghép D0,D1 thành Int32:

static int ToInt32BE(ushort hi, ushort lo)
{
    return (hi << 16) | lo;
}

// Float 32-bit (IEEE 754) big-endian word order:
static float ToFloatBE(ushort hi, ushort lo)
{
    uint raw = ((uint)hi << 16) | lo;
    unsafe { return *(float*)&raw; }
}

