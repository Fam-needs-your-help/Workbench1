using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.IO.Ports;
using System.Collections.Generic;
using System;
using comPortTester.Infra;
using comPortTester.Util;
using comPortTester.Domain;
using System.Threading.Tasks;
namespace comPortTester.Service
{
    public class DongleManagerService
    {
        private ObservableCollection<string> _comPortList;
        private ReadOnlyObservableCollection<string> _readOnlyComPortList;
        private uint testCounter;
        public ReadOnlyObservableCollection<string> ComPortList { get { return _readOnlyComPortList; } }

        private ObservableCollection<DongleService> _dongleServices;
        public ReadOnlyObservableCollection<DongleService> DongleServices;
        public DongleService CreateDongleFromComPort(string comPortName)
        {
            ISerialDriver serialDriver;
            try
            {
                serialDriver = new SerialDriver(comPortName);
            }
            catch
            {
                return null;
            }
            ICommandService commandService = new CommandService(serialDriver, new CobsDataFramer());
            
            DongleService dongleService = new DongleService(commandService, comPortName);
            _dongleServices.Add(dongleService);
            return dongleService;
        }

        public DongleManagerService()
        {
            _comPortList = new ObservableCollection<string>();
            _readOnlyComPortList = new ReadOnlyObservableCollection<string>(_comPortList);
            _dongleServices = new ObservableCollection<DongleService>();
            testCounter = 1;
            DongleServices = new ReadOnlyObservableCollection<DongleService>(_dongleServices);
            StartTimerForComPortListUpdate();
        }

        public Task StartTestsForAllDongle()
        {
            List<Task> tasks = new List<Task>();
            foreach(DongleService dongleService in _dongleServices)
            {
                tasks.Add(StartTestsForOneDongle(dongleService));
            }
            return Task.WhenAll(tasks.ToArray());
        }

        private async Task StartTestsForOneDongle(DongleService dongleService)
        {
            Console.WriteLine("Dongle:" + dongleService.Dongle.ComPortName);
            foreach (DeviceModel device in dongleService.ConnectedDevices)
            {
                if (device.TestType != TestResultModel.TestType.Idle)
                {
                    Console.WriteLine("Start " + device.TestType + " test for device: " + device.Address.ToString());
                    await dongleService.StartTest(device.Address, device.TestType, device.FrameSize);
                }
            }
        }

        public Task StartResultsCollectionForAllDongle()
        {
            List<Task> tasks = new List<Task>();
            foreach (DongleService dongleService in _dongleServices)
            {
                tasks.Add(StartResultCollectionForOneDongle(dongleService));
            }
            return Task.WhenAll(tasks.ToArray());
        }

        private async Task StartResultCollectionForOneDongle(DongleService dongleService)
        {
            Console.WriteLine("Dongle:" + dongleService.Dongle.ComPortName);
            foreach (DeviceModel device in dongleService.ConnectedDevices)
            {
                if (device.TestType != TestResultModel.TestType.Idle)
                {
                    Console.WriteLine("Start " + " result collection for device: " + device.Address.ToString());
                    await dongleService.StartResultCollection(device.Address, device.TestType);
                }
            }
        }

        public Task StopResultsCollectionForAllDongle()
        {
            List<Task> tasks = new List<Task>();
            foreach (DongleService dongleService in _dongleServices)
            {
                tasks.Add(StopResultCollectionForOneDongle(dongleService));
            }
            return Task.WhenAll(tasks.ToArray());
        }

        private async Task StopResultCollectionForOneDongle(DongleService dongleService)
        {
            Console.WriteLine("Dongle:" + dongleService.Dongle.ComPortName);
            foreach (DeviceModel device in dongleService.ConnectedDevices)
            {
                if (device.TestType != TestResultModel.TestType.Idle)
                {
                    Console.WriteLine("Stop " + " result collection for device: " + device.Address.ToString());
                    await dongleService.StopResultCollection(device.Address);
                }
                else
                {
                    device.TestResults.Add(new TestResultModel(TestResultModel.TestType.Idle));
                }

            }
        }

        public Task StartDataWriteForAllDongle()
        {
            List<Task> tasks = new List<Task>();
            foreach (DongleService dongleService in _dongleServices)
            {
                tasks.Add(StartDataWriteForOneDongle(dongleService));
            }
            return Task.WhenAll(tasks.ToArray());
        }

        private async Task StartDataWriteForOneDongle(DongleService dongleService)
        {
            Console.WriteLine("Dongle:" + dongleService.Dongle.ComPortName);
            foreach (DeviceModel device in dongleService.ConnectedDevices)
            {
                if (device.TestType != TestResultModel.TestType.Idle && device.TestType != TestResultModel.TestType.Downlink)
                {
                    Console.WriteLine("Start data write for device: " + device.Address.ToString());
                    await dongleService.StartDataWrite(device.Address);
                }
            }
        }

        public Task StopDataWriteForAllDongle()
        {
            List<Task> tasks = new List<Task>();
            foreach (DongleService dongleService in _dongleServices)
            {
                tasks.Add(StopDataWriteForOneDongle(dongleService));
            }
            return Task.WhenAll(tasks.ToArray());
        }

        private async Task StopDataWriteForOneDongle(DongleService dongleService)
        {
            Console.WriteLine("Dongle:" + dongleService.Dongle.ComPortName);
            foreach (DeviceModel device in dongleService.ConnectedDevices)
            {
                if (device.TestType != TestResultModel.TestType.Idle && device.TestType != TestResultModel.TestType.Downlink)
                {
                    Console.WriteLine("Start data write for device: " + device.Address.ToString());
                    await dongleService.StopDataWrite(device.Address);
                }
            }
        }

        public Task StopTestsForAllDongle()
        {
            List<Task> tasks = new List<Task>();
            foreach (DongleService dongleService in _dongleServices)
            {
                tasks.Add(StopTestsForOneDongle(dongleService));
            }
            testCounter++;
            return Task.WhenAll(tasks.ToArray());
        }

        private async Task StopTestsForOneDongle(DongleService dongleService)
        {
            Console.WriteLine("Dongle:" + dongleService.Dongle.ComPortName);
            foreach (DeviceModel device in dongleService.ConnectedDevices)
            {
                if (device.TestType != TestResultModel.TestType.Idle)
                {
                    Console.WriteLine("Stop test for device:" + device.Address.ToString());
                    await dongleService.StopTestMode(device.Address);
                }
            }
        }

        private DispatcherTimer _timer;

        private void StartTimerForComPortListUpdate()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += new EventHandler(UpdateComPortList);
            _timer.Interval = new TimeSpan(0, 0, 1);
            _timer.Start();
        }

        private void UpdateComPortList(object sender, EventArgs e)
        {
            string[] comPortNames = SerialPort.GetPortNames();
            Boolean namesChanged = false;
            if(_comPortList.Count == comPortNames.Length)
            {
                for(int i = 0; i < comPortNames.Length;i ++)
                {
                    if(!comPortNames[i].Equals(_comPortList[i]))
                    {
                        namesChanged = true;
                        break;
                    }
                }
            }
            else
            {
                namesChanged = true;
            }

            if (namesChanged)
            {
                _comPortList.Clear();
                foreach (string portName in SerialPort.GetPortNames())
                {
                    _comPortList.Add(portName);
                }
            }

        }
    }
}
