using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SimoMaster
{
    public partial class RegisterMappingWindow : Window
    {
        public RegisterMap Map { get; private set; }

        public RegisterMappingWindow(RegisterMap currentMap)
        {
            InitializeComponent();

            txtStatus.Text = currentMap.StatusWord.ToString();
            txtCurrent.Text = currentMap.CurrentStart.ToString();
            txtVoltage.Text = currentMap.VoltageStart.ToString();
            txtP.Text = currentMap.ActivePower.ToString();
            txtS.Text = currentMap.ApparentPower.ToString();

            txtCoilOpen.Text = currentMap.CoilOpen.ToString();
            txtCoilClose.Text = currentMap.CoilClose.ToString();
            txtCoilStart.Text = currentMap.CoilStart.ToString();
            txtCoilStop.Text = currentMap.CoilStop.ToString();
            txtCoilFault.Text = currentMap.CoilFault.ToString();

            // 🔥 SET TOGGLE
            if (currentMap.PlcBaseAddressing)
                rbPlc.IsChecked = true;
            else
                rbZero.IsChecked = true;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            Map = new RegisterMap
            {
                StatusWord = ushort.Parse(txtStatus.Text),
                CurrentStart = ushort.Parse(txtCurrent.Text),
                VoltageStart = ushort.Parse(txtVoltage.Text),
                ActivePower = ushort.Parse(txtP.Text),
                ApparentPower = ushort.Parse(txtS.Text),

                CoilOpen = ushort.Parse(txtCoilOpen.Text),
                CoilClose = ushort.Parse(txtCoilClose.Text),
                CoilStart = ushort.Parse(txtCoilStart.Text),
                CoilStop = ushort.Parse(txtCoilStop.Text),
                CoilFault = ushort.Parse(txtCoilFault.Text),

                // 🔥 SIMPAN MODE
                PlcBaseAddressing = rbPlc.IsChecked == true
            };

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}