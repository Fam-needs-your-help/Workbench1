using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace comPortTester.Domain
{
    public class DongleModel : ObservableObject
    {
        public enum DongleState
        {
            Connected,
            Scanning,
            Error
        }

        private String _comPortName;
        private DongleState _state;

        public DongleModel(string comPortName)
        {
            ComPortName = comPortName;
            State = DongleState.Connected;
        }

        public string ComPortName
        {
            get => _comPortName;
            set => _comPortName = value;
        }

        public DongleState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }
    }
}
