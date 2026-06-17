using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
// Thêm thư viện này để dùng được SerialPort (Cổng COM)
using System.IO.Ports;

namespace vimachbandan
{
    public partial class Form1 : Form
    {
        // Khai báo biến quản lý cổng Serial và chuỗi nhận dữ liệu
        private SerialPort serialPort;
        private string receivedData = string.Empty;

        public Form1()
        {
            InitializeComponent();
            serialPort = new SerialPort();
            // Đăng ký sự kiện tự động chạy khi có dữ liệu từ ESP32 gửi về
            serialPort.DataReceived += SerialPort_DataReceived;
        }

        // Khi Form bắt đầu chạy, tự động tìm các cổng Bluetooth (COM) đang có
        private void Form1_Load(object sender, EventArgs e)
        {
            LoadAvailablePorts();
        }

        // Hàm lấy danh sách các cổng COM
        private void LoadAvailablePorts()
        {
            string[] ports = SerialPort.GetPortNames();
            cmbPort.Items.Clear();
            cmbPort.Items.AddRange(ports);
            if (cmbPort.Items.Count > 0) cmbPort.SelectedIndex = 0;
        }

        // Xử lý nút Kết nối / Ngắt kết nối
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen)
            {
                try
                {
                    if (cmbPort.SelectedItem == null)
                    {
                        MessageBox.Show("Vui lòng chọn một cổng COM/Bluetooth!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    serialPort.PortName = cmbPort.SelectedItem.ToString();
                    serialPort.BaudRate = 9600; // Tốc độ phải khớp với ESP32
                    serialPort.Open();

                    btnConnect.Text = "Ngắt Kết Nối";
                    lblStatus.Text = "Trạng thái: Đã kết nối Bluetooth!";
                    EnableControls(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể kết nối: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                DisconnectSerial();
            }
        }

        // Hàm ngắt kết nối an toàn
        private void DisconnectSerial()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    // Hủy đăng ký sự kiện trước khi đóng để tránh lỗi đụng độ Thread (Deadlock)
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                    serialPort.DataReceived += SerialPort_DataReceived; // Đăng ký lại cho lần kết nối sau
                }
            }
            catch { }

            btnConnect.Text = "Kết Nối";
            lblStatus.Text = "Trạng thái: Mất kết nối.";
            ResetDisplay();
            EnableControls(false);
        }

        // Bật/Tắt các nút điều khiển dựa vào trạng thái kết nối
        private void EnableControls(bool isConnected)
        {
            btnTurnOn.Enabled = isConnected;
            btnTurnOff.Enabled = isConnected;
            cmbPort.Enabled = !isConnected;
        }

        // Hàm xóa dữ liệu hiển thị khi ngắt kết nối hoặc tắt hệ thống
        private void ResetDisplay()
        {
            lblTemp.Text = "-- C";
            lblHumi.Text = "-- %";
        }

        // GỬI LỆNH BẬT TOÀN BỘ HỆ THỐNG (Gửi ký tự '1' xuống ESP32)
        private void btnTurnOn_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Write("1");
                // CẬP NHẬT: Khớp với logic điều khiển 4 LED của ESP32 mới 2026
                lblStatus.Text = "Trạng thái: Đã ra lệnh BẬT HỆ THỐNG (LED 1, 2, 4 Sáng | LED 3 Chớp tắt).";
            }
        }

        // GỬI LỆNH TẮT TOÀN BỘ HỆ THỐNG (Gửi ký tự '0' xuống ESP32)
        private void btnTurnOff_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Write("0");
                // CẬP NHẬT: Khớp với logic tắt toàn bộ hệ thống
                lblStatus.Text = "Trạng thái: Đã ra lệnh TẮT HỆ THỐNG (Tắt tất cả LED & Màn hình).";
                ResetDisplay();
            }
        }

        // Tự động chạy ngầm để đón dữ liệu ESP32 gửi lên
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    receivedData = serialPort.ReadLine();

                    // Chuyển dữ liệu về Thread giao diện để hiển thị công khai
                    this.Invoke(new Action(() => {
                        ParseAndDisplayData(receivedData);
                    }));
                }
            }
            catch { }
        }

        // Hàm phân tách chuỗi dữ liệu định dạng: "Nh iet do: 28.5C | Do am: 65.0%"
        private void ParseAndDisplayData(string data)
        {
            try
            {
                // CẬP NHẬT: Khớp chính xác ký tự "C" viết hoa và dấu cách từ lệnh SerialBT.print của ESP32
                if (data.Contains("Nhiet do:") && data.Contains("Do am:"))
                {
                    string[] parts = data.Split('|');

                    // Cắt chuỗi lấy giá trị nhiệt độ (Ví dụ tách từ "Nhiet do: 28.5C " thành "28.5C")
                    string temp = parts[0].Replace("Nhiet do:", "").Trim();
                    // Cắt chuỗi lấy giá trị độ ẩm (Ví dụ tách từ " Do am: 65.0%" thành "65.0%")
                    string humi = parts[1].Replace("Do am:", "").Trim();

                    lblTemp.Text = temp;
                    lblHumi.Text = humi;
                }
            }
            catch { }
        }

        // Các hàm click của các label (để trống phòng trường hợp nhấp đúp nhầm bên giao diện)
        private void lblTemp_Click(object sender, EventArgs e) { }
        private void lblHumi_Click(object sender, EventArgs e) { }
        private void lblStatus_Click(object sender, EventArgs e) { }

        // Tự động chạy khi bạn tắt app để không bị kẹt hay treo cổng COM/Bluetooth
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisconnectSerial();
        }
    }
}