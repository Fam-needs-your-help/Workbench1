			
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel.Design;

namespace comPortTesterEX
{
    public class Dongle
    {   
        //Declare Variables
        private SerialPort port;
        private Boolean isSerialPortOpen = false; // reflects current IsOpen() value
        private string _comPortName;
        private byte[] receivedpackage = new byte[SLIP.receivedPackageLength];
        List<string> lists = new List<string>();
        public ObservableCollection<string> bdAddrs;
        public List<string> results { get; set; }
        public string status { get; set; }
        public string comPortName { get { return _comPortName; } }

        public static int WriteTimeout = 500;
        public static bool SendDataSuccess = false, SendPackageReturnOK = false;
        public static byte[] PortReceivedDataBuffer = new byte[0];

        //Declare Constructors
        public Dongle(String comPortName)
        {
            _comPortName = comPortName;
            results = new List<string>();
            bdAddrs = new ObservableCollection<string>(lists);
            status = "";
            SetComPort();
        }

        //Declare Functions
        public void SetComPort()
        {
            port = new SerialPort()
            {
                PortName = _comPortName,
                BaudRate = 115200,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                RtsEnable = true,
                DtrEnable = true,
            };
            port.DataReceived += new SerialDataReceivedEventHandler(DataReceived);
        }

        public void OpenComPort()
        {
            try
            {
                if (isSerialPortOpen == true)
                    System.Diagnostics.Trace.WriteLine("Port is already open");
                else if (isSerialPortOpen == false)
                {
                    port.Open();  //if COMport is closed, open COMport
                    isSerialPortOpen = true;
                }
            }
            catch (UnauthorizedAccessException SerialException)
            {
                //Message operating system denies access to open the serial port because of an I/O error or a specific type of security error.
                MessageBox.Show(SerialException.ToString());
                port.Close();
            }
            catch (System.IO.IOException SerialException)
            {
                //Message requested port is used by another process or program
                MessageBox.Show(SerialException.ToString());
                port.Close();
            }
            catch (InvalidOperationException SerialException)
            {
                //Message operation on the port that is not permitted
                MessageBox.Show(SerialException.ToString());
                port.Close();
            }
            catch
            {
                //Any other error. "Opening in opening serial port", ComPortName1, "Unknown Error".
                MessageBox.Show("Opening in opening serial port" + port.PortName + "Unknown Error.");
                port.Close();
            }
        }

        public void CloseComPort()
        {
            if (isSerialPortOpen == false)
                System.Diagnostics.Trace.WriteLine("Port is already closed");
            else if (isSerialPortOpen == true)
            {
                port.Close();  //if COMport is closed, open COMport
                isSerialPortOpen = false;
                System.Diagnostics.Trace.WriteLine("Port has been closed");
            }
            else { System.Diagnostics.Trace.WriteLine("Error Detected here");}
        }

        public void StartScan()
        {
           //WriteDataToPort [0x00]
           byte[] StartPrompt = {0x00};
           WriteDataToPort(StartPrompt);
        }

        public void StopScan()
        {
            //WriteDataToPort [0x01]
            byte[] StopPrompt = { 0x01 };
            WriteDataToPort(StopPrompt);
            lists.Clear();
            bdAddrs.Clear();
            //System.Diagnostics.Trace.WriteLine(_comPortName + " has stopped");
        }

        private void WriteDataToPort(byte[] packagetosend)
        {
            //OpenComPort();
            port.WriteTimeout = 500;
            SendDataSuccess = false;
            SendPackageReturnOK = false;
            try
            {
                if (port.IsOpen && packagetosend.Length > 0)
                {
                    //Direct send package to COM Port
                    port.Write(packagetosend, 0, packagetosend.Length);
                    SendDataSuccess = true;
                }
                else
                {
                    SendDataSuccess = false;
                }
            }
            catch (TimeoutException SerialTimeOutException)
            {
                MessageBox.Show(SerialTimeOutException.ToString());
                port.Close();
            }
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string DataReceivedPortName = sp.PortName;
            int receivedbytes1 = sp.BytesToRead;
            PortReceivedDataBuffer = new byte[receivedbytes1];
            sp.Read(PortReceivedDataBuffer, 0, receivedbytes1); //read data byte from COMport1

                //Direct send package bypassing SLIP layer
                receivedpackage = PortReceivedDataBuffer;

            String datatoreceive = BitConverter.ToString(receivedpackage);
            sixStringSplitter(datatoreceive);
            
            //update Data Table --
            //* uSBID --- bDAddress[6 bytes] --- rSSI[1 byte]
            //* echoSuccessRate, testCase2Result [3 byts]
            //* testCase1Result, testCase3Result [3 bytes]
        }

        private void sixStringSplitter(string target) //  Splits the string into iterations of XX-XX-XX-XX-XX-XX
        {
            int a = 17;
            for (int i = 0; i < target.Length; i += 18)
            {
                if (((i + a) >= target.Length) == true) { lists.Add(target.Substring(i, 17)); }
                else 
                {  
                    lists.Add(target.Substring(i, a));
                    bdAddrs.Add(target.Substring(i, a));
                }
            }
            for (int i = 0; i < lists.Count; i++)
                Console.WriteLine(lists[i]);
        }
    }
}
