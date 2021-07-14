using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using comPortTester.Domain;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Threading;
using comPortTester.Util;

namespace comPortTester.Service
{
    public class DongleService
    {
        private ObservableCollection<DeviceModel> _devices;
        private ReadOnlyObservableCollection<DeviceModel> _readOnlyDevices;
        private ObservableCollection<DeviceModel> _scannedDevices;
        private ReadOnlyObservableCollection<DeviceModel> _readOnlyScannedDevices;
        private DongleModel _dongleModel;
        private uint _testCounter;
        private ICommandService _commandService;
        private SynchronizationContext _uiContext;

        public DongleService(ICommandService commandService, string comPortName)
        {
            _uiContext = SynchronizationContext.Current;
            _devices = new ObservableCollection<DeviceModel>();
            _readOnlyDevices = new ReadOnlyObservableCollection<DeviceModel>(_devices);
            _testCounter = new uint();
            _scannedDevices = new ObservableCollection<DeviceModel>();
            _readOnlyScannedDevices = new ReadOnlyObservableCollection<DeviceModel>(_scannedDevices);

            _dongleModel = new DongleModel(comPortName);
            _commandService = commandService;
            ObserveCommandServiceEvents();
        }

        public DongleModel Dongle
        {
            get => _dongleModel;
            private set => _dongleModel = value;
        }

        public ReadOnlyObservableCollection<DeviceModel> ConnectedDevices { get { return _readOnlyDevices; } }
        public ReadOnlyObservableCollection<DeviceModel> ScannedDevices { get { return _readOnlyScannedDevices; } }

        private void ObserveCommandServiceEvents()
        {
            _commandService.DeviceScanned += NewDeviceScanned;
            _commandService.DeviceStatusChanged += DeviceStatusChangeHandler;
        }

        private void NewDeviceScanned(object sender, NewDeviceScannedEventArgs e)
        {
            string serialNumber = SerialNumberDecoder.FromAdvertisingInfo(e.SerialNumber);
            DeviceModel newDevice = new DeviceModel(MapBtAddress(e.Addr), serialNumber);
            _uiContext.Send(x => _scannedDevices.Add(newDevice), null);
        }

        private void DeviceStatusChangeHandler(object sender, DeviceStatusChangedEventArgs e)
        {
            DeviceModel.BluetoothAddress bluetoothAddress = MapBtAddress(e.Addr);
            DongleResponse newState = e.Response;
            DeviceModel device = GetDeviceFromAddress(bluetoothAddress);
            if (device == null)
            {
                return;
            }
            Console.WriteLine("New state: " + newState.ToString());
            switch (newState)
            {
                case DongleResponse.DEVICE_CONNECT_FAILED:
                    device.State = DeviceModel.DeviceState.Disconnected;
                    _uiContext.Send(x => _devices.Remove(device), null);
                    break;
                case DongleResponse.DEVICE_DISCONNECTED:
                    device.State = DeviceModel.DeviceState.Disconnected;
                    _uiContext.Send(x => _devices.Remove(device), null);
                    break;
                case DongleResponse.DEVICE_BONDED:
                    DeviceModel.BluetoothAddress newBluetoothAddress = MapBtAddress(e.NewAddr);
                    device.Address = newBluetoothAddress;
                    break;
                case DongleResponse.DEVICE_SUBSCRIBED:
                    device.State = DeviceModel.DeviceState.Connected;
                    break;
                default:
                    Console.WriteLine("Not handled state");
                    break;
            }
        }

        private DeviceModel GetDeviceFromAddress(DeviceModel.BluetoothAddress bluetoothAddress)
        {
            foreach(DeviceModel device in ConnectedDevices)
            {
                if (device.Address.Equals(bluetoothAddress))
                {
                    return device;
                }
            }
            return null;
        }

        public async Task StartScan()
        {
            CommandResponse rsp = await _commandService.StartScanAsync();

            if (rsp.Status == CommandStatus.OK)
            {
                _scannedDevices.Clear();
                Dongle.State = DongleModel.DongleState.Scanning;
            }
            else
            {
                Dongle.State = DongleModel.DongleState.Error;
            }
        }

        public async Task StopScan()
        {
            CommandResponse rsp = await _commandService.StopScanAsync();
            
            if (rsp.Status == CommandStatus.OK)
            {
                Dongle.State = DongleModel.DongleState.Connected;
            }
            else
            {
                Dongle.State = DongleModel.DongleState.Error;
            }
        }

        private BtAddress MapBluetoothAddress(DeviceModel.BluetoothAddress address)
        {
            BtAddress btAddr = new BtAddress();
            btAddr.Val = address.Address;
            btAddr.Type = address.Type == DeviceModel.BluetoothAddress.AddressType.Public ?
                BtAddressType.Public : BtAddressType.Random;

            return btAddr;
        }

        private DeviceModel.BluetoothAddress MapBtAddress(BtAddress btAddr)
        {
            DeviceModel.BluetoothAddress bluetoothAddress = new DeviceModel.BluetoothAddress();
            bluetoothAddress.Address = btAddr.Val;
            bluetoothAddress.Type = btAddr.Type == BtAddressType.Public ?
                DeviceModel.BluetoothAddress.AddressType.Public :
                DeviceModel.BluetoothAddress.AddressType.Random;

            return bluetoothAddress;
        }

        public async Task ConnectDevice(DeviceModel device)
        {
            BtAddress btAddr = MapBluetoothAddress(device.Address);
            device.State = DeviceModel.DeviceState.Connecting;
            CommandResponse rsp = await _commandService.ConnectAsync(btAddr);
            if (rsp.Status == CommandStatus.OK && rsp.DongleRsp == DongleResponse.CMD_OK)
            {
                _devices.Add(device);
            }
            else
            {
                Dongle.State = DongleModel.DongleState.Error;
            }
        }

        public async Task DisconnectDevice(DeviceModel.BluetoothAddress address)
        {
            DeviceModel deviceModel = GetDeviceModelFromAddress(address);
            if (deviceModel != null)
            {
                BtAddress btAddr = MapBluetoothAddress(address);
                CommandResponse rsp = await _commandService.DisconnectAsync(btAddr);
                if (rsp.Status == CommandStatus.OK && rsp.DongleRsp == DongleResponse.CMD_OK)
                {
                    deviceModel.State = DeviceModel.DeviceState.Disconnecting;
                }
                else
                {
                    Dongle.State = DongleModel.DongleState.Error;
                }
            }
        }

        public async Task StartTest(DeviceModel.BluetoothAddress address, TestResultModel.TestType testType, byte framesize)
        {
            DeviceModel deviceModel = GetDeviceModelFromAddress(address);
            if (deviceModel == null)
                return;

            BtAddress btAddr = MapBluetoothAddress(address);
            CommandResponse rsp;
            WriteCtrlAsyncResponse asyncRsp;
            byte[] asyncRspData;
            Task<WriteCtrlAsyncResponse> asyncResponseTask;

            asyncResponseTask = _commandService.WaitForResponseAsync(btAddr);
            // 0xFA is START_TEST_MODE command ,but 0x11 to 0x66 are fake bluetooth address
            // to be replaced with dongle address
            // TODO update here
            byte[] startTestModeCmd = { 0xFA, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            rsp = await _commandService.WriteCtrlAsync(btAddr, startTestModeCmd);
            if (rsp.Status != CommandStatus.OK && rsp.DongleRsp != DongleResponse.CMD_OK)
            {
                Dongle.State = DongleModel.DongleState.Error;
            }

            deviceModel.State = DeviceModel.DeviceState.TestMode;

            asyncRsp = await asyncResponseTask;
            asyncRspData = asyncRsp.Data;
            if(asyncRspData[0] != 0xFA)
            {
                Console.WriteLine("Start test mode failed");
                return;
            }
            Console.WriteLine("Device: " + address.ToString() + " started Test Mode.");

            asyncResponseTask = _commandService.WaitForResponseAsync(btAddr);
            byte testModeCommandId = GetTestCommandIdFromTestType(testType);
            byte[] enterTestModeCmd;
            if(testType == TestResultModel.TestType.Downlink)
            {
                enterTestModeCmd = new byte[]{ 0xFC, testModeCommandId, framesize };
            }
            else
            {
                enterTestModeCmd = new byte[] { 0xFC, testModeCommandId };
            }
            rsp = await _commandService.WriteCtrlAsync(btAddr, enterTestModeCmd);
            if (rsp.Status != CommandStatus.OK && rsp.DongleRsp != DongleResponse.CMD_OK)
            {
                Dongle.State = DongleModel.DongleState.Error;
            }

            asyncRsp = await asyncResponseTask;
            asyncRspData = asyncRsp.Data;
            if(asyncRspData[0] != 0xFC)
            {
                Console.WriteLine("Change test mode failed");
                return;
            }
            Console.WriteLine("Device: " + address.ToString() + " changed Test Mode.");

            deviceModel.State = GetDeviceStateFromTestType(testType);
        }

        private DeviceModel.DeviceState GetDeviceStateFromTestType(TestResultModel.TestType testType)
        {
            DeviceModel.DeviceState deviceState;
            switch(testType)
            {
                case TestResultModel.TestType.Uplink:
                    deviceState = DeviceModel.DeviceState.UplinkTest;
                    break;
                case TestResultModel.TestType.Bidirectional:
                    deviceState = DeviceModel.DeviceState.BidirectionalTest;
                    break;
                case TestResultModel.TestType.Downlink:
                    deviceState = DeviceModel.DeviceState.DownlinkTest;
                    break;
                default:
                    deviceState = DeviceModel.DeviceState.UplinkTest;
                    break;
            }
            return deviceState;
        }

        public async Task StartResultCollection(DeviceModel.BluetoothAddress address, TestResultModel.TestType testType)
        {
            DeviceModel deviceModel = GetDeviceModelFromAddress(address);
            if (deviceModel == null)
                return;

            BtAddress btAddr = MapBluetoothAddress(address);
            CommandResponse rsp;
            ResultCollectionType type = GetResultCollectionTypeFromTestType(testType);

            rsp = await _commandService.StartResultAsync(btAddr, type);
            if (rsp.Status != CommandStatus.OK && rsp.DongleRsp != DongleResponse.CMD_OK)
            {
                Dongle.State = DongleModel.DongleState.Error;
            }
        }

        private ResultCollectionType GetResultCollectionTypeFromTestType(TestResultModel.TestType testType)
        {
            ResultCollectionType resultCollectionType;
            switch(testType)
            {
                case TestResultModel.TestType.Uplink:
                    resultCollectionType = ResultCollectionType.Tx;
                    break;
                case TestResultModel.TestType.Bidirectional:
                    resultCollectionType = ResultCollectionType.Bidirectional;
                    break;
                case TestResultModel.TestType.Downlink:
                    resultCollectionType = ResultCollectionType.Rx;
                    break;
                default:
                    resultCollectionType = 0x00;
                    break;
            }
            return resultCollectionType;
        }

        private byte GetTestCommandIdFromTestType(TestResultModel.TestType testType)
        {
            byte commandId;
            switch(testType)
            {
                case TestResultModel.TestType.Uplink:
                    commandId = 0x00;
                    break;
                case TestResultModel.TestType.Bidirectional:
                    commandId = 0x01;
                    break;
                case TestResultModel.TestType.Downlink:
                    commandId = 0x02;
                    break;
                default:
                    commandId = 0x00;
                    break;
            }
            return commandId;
        }

        public async Task StopResultCollection(DeviceModel.BluetoothAddress address)
        {
            DeviceModel deviceModel = GetDeviceModelFromAddress(address);
            if (deviceModel == null)
                return;

            BtAddress btAddr = MapBluetoothAddress(address);
            StopResultCommandResponse rsp;

            rsp = await _commandService.StopResultAsync(btAddr);
            if (rsp.CmdRsp.Status != CommandStatus.OK && rsp.CmdRsp.DongleRsp != DongleResponse.CMD_OK)
            {
                Dongle.State = DongleModel.DongleState.Error;
            }
            uint throughput = ThroughputFromResultBytes(rsp.Data);
            TestResultModel testResult = new TestResultModel(deviceModel.TestType)
            {
                FrameSize = deviceModel.FrameSize,
                ThroughputBps = throughput
            };

            deviceModel.TestResults.Add(testResult);
        }

        public async Task StartDataWrite(DeviceModel.BluetoothAddress address)
        {
            DeviceModel deviceModel = GetDeviceModelFromAddress(address);
            if (deviceModel == null)
                return;

            BtAddress btAddr = MapBluetoothAddress(address);
            CommandResponse rsp;

            rsp = await _commandService.WriteDataAsync(btAddr, deviceModel.FrameSize, GetDataWriteTypeFromTestType(deviceModel.TestType));
            if (rsp.Status != CommandStatus.OK && rsp.DongleRsp != DongleResponse.CMD_OK)
            {
                Dongle.State = DongleModel.DongleState.Error;
            }
        }

        private DataWriteType GetDataWriteTypeFromTestType(TestResultModel.TestType testType)
        {
            DataWriteType dataWriteType;
            switch(testType)
            {
                case TestResultModel.TestType.Uplink:
                    dataWriteType = DataWriteType.Tx;
                    break;
                case TestResultModel.TestType.Bidirectional:
                    dataWriteType = DataWriteType.Bidirectional;
                    break;
                default:
                    dataWriteType = DataWriteType.Tx;
                    break;
            }
            return dataWriteType;
        }

        public async Task StopDataWrite(DeviceModel.BluetoothAddress address)
        {
            DeviceModel deviceModel = GetDeviceModelFromAddress(address);
            if (deviceModel == null)
                return;

            BtAddress btAddr = MapBluetoothAddress(address);
            CommandResponse rsp;

            rsp = await _commandService.StopDataAsync(btAddr);
            if (rsp.Status != CommandStatus.OK && rsp.DongleRsp != DongleResponse.CMD_OK)
            {
                Dongle.State = DongleModel.DongleState.Error;
            }
        }

        private uint ThroughputFromResultBytes(byte[] resultBytes)
        {
            uint durationInMs = BitConverter.ToUInt32(resultBytes, 0);
            uint bytes = BitConverter.ToUInt32(resultBytes, 4);
            return (bytes / (durationInMs/1000));
        }

        public async Task StopTestMode(DeviceModel.BluetoothAddress address)
        {
            DeviceModel deviceModel = GetDeviceModelFromAddress(address);
            if (deviceModel == null)
                return;

            BtAddress btAddr = MapBluetoothAddress(address);
            CommandResponse rsp;
            WriteCtrlAsyncResponse asyncRsp;
            byte[] asyncRspData;
            Task<WriteCtrlAsyncResponse> asyncResponseTask;

            asyncResponseTask = _commandService.WaitForResponseAsync(btAddr);
            byte[] stopTestModeCmd = { 0xFE };
            rsp = await _commandService.WriteCtrlAsync(btAddr, stopTestModeCmd);
            if (rsp.Status != CommandStatus.OK && rsp.DongleRsp != DongleResponse.CMD_OK)
            {
                Dongle.State = DongleModel.DongleState.Error;
            }

            asyncRsp = await asyncResponseTask;
            asyncRspData = asyncRsp.Data;
            if (asyncRspData[0] != 0xFE)
            {
                Console.WriteLine("Stop test mode failed");
                return;
            }

            deviceModel.State = DeviceModel.DeviceState.Connected;
        }

        private DeviceModel GetDeviceModelFromAddress(DeviceModel.BluetoothAddress address)
        {
            foreach(DeviceModel device in _devices)
            {
                if(device.Address.Equals(address))
                {
                    return device;
                }
            }
            return null;
        }

        public void Close()
        {
            _commandService.Close();
        }
    }
}
