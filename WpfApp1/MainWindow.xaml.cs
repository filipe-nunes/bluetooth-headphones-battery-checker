using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Windows.Forms.NotifyIcon ni;

        private NetworkStream stream;

        BluetoothClient client;
        BluetoothDeviceInfo jblHeadphones;

        public MainWindow()
        {
            InitializeComponent();

            var iconStream = System.Windows.Application.GetResourceStream(new Uri("/logo.ico", UriKind.Relative)).Stream;

            ni = new System.Windows.Forms.NotifyIcon();
            ni.Icon = new Icon(iconStream);
            ni.Visible = true;
            ni.DoubleClick +=
            delegate (object sender, EventArgs args)
            {
                this.Show();
                this.WindowState = WindowState.Normal;
            };

            MenuItem itemRefresh = new MenuItem("Refresh", contextRefresh);
            MenuItem itemClose = new MenuItem("Close", close);
            MenuItem[] items =  new MenuItem[2];
            items[0] = itemRefresh;
            items[1] = itemClose;
            ni.ContextMenu = new ContextMenu(items);
            ni.Text = "Not connected";
            ni.BalloonTipTitle = "JBL E45BT";
            Button_Click();

            Closing += close;
        }

        private void contextRefresh(object sender, EventArgs e)
        {
            Button_Click();
            if (!string.IsNullOrEmpty(ni.Text))
            {
                ni.ShowBalloonTip(2);
            }
        }

        private void close(object sender, EventArgs e)
        {
            ni.Visible = false;
            //System.Windows.Application.Current.Shutdown();
            Environment.Exit(0);
        }

        private void Button_Click(object sender = null, RoutedEventArgs e = null)
        {
            client = new BluetoothClient();

            BluetoothDeviceInfo[] devices = client.DiscoverDevices(255, false, true, false, false);
            jblHeadphones = devices.Where(d => d.DeviceName.Equals("JBL E45BT")).FirstOrDefault();
     
            if (jblHeadphones != null)
            {
                headphonesName.Content = jblHeadphones.DeviceName;
                if(!jblHeadphones.Connected)
                {
                    headphonesName.Foreground = System.Windows.Media.Brushes.Red;
                    ni.Text = "Not connected";
                }
                else if (jblHeadphones.Authenticated)
                {
                    headphonesName.Foreground = System.Windows.Media.Brushes.Green;

                    /*
                        Handsfree   0x111E  Hands - Free Profile(HFP)
                        NOTE: Used as both Service Class Identifier and Profile Identifier.	Service Class / Profile

                        Headset	0x1108	Headset Profile (HSP)
                        NOTE: Used as both Service Class Identifier and Profile Identifier.	Service Class/ Profile

                    */

                    client.Connect(jblHeadphones.DeviceAddress, BluetoothService.Handsfree);

                    stream = client.GetStream();
                    stream.ReadTimeout = 5000;
                      


                    // file:///D:/Filipe/Downloads/HFP_SPEC_V16.pdf#page=19&zoom=100,0,568
                    // secção 4 
                    // ler a abateria: AT+CBC=?  https://radekp.github.io/qtmoko/api/modememulator-controlandstatus.html

                    string msgFromJBL = "";

                    read(); // AT+BRSF=<HF Supported Features>
                    write("+BRSF:512\r");
                    write("OK\r");

                    read(); // AT+CIND=?
                    write("+CIND:battchg\r");
                    write("OK\r");

                    read(); // AT+CIND?
                    write("+CIND:battchg,1\r");
                    write("OK\r");

                    read(); // AT+CMER=3,0,0,1
                    write("OK\r");

                    read(); // AT+CHLD=?
                    write("+CHLD:\r");
                    write("OK\r");


                    read(); // AT+CMEE=1
                    write("OK\r");

                    read(); // AT+BRTH?
                    write("OK\r");

                    read(); // AT+CLIP=1
                    write("OK\r");

                    read(); // AT+CCWA=1
                    write("OK\r");


                    read(); // AT+XAPL=0AC8-9621-0100,3
                    write("+XAPL=windows,3\r");

                    read(); // AT+XEVENT=0,0
                    write("OK\r");

                    read(); // AT+NREC=0
                    write("OK\r");

                    read(); // AT+VGS=0
                    write("OK\r");

                    msgFromJBL = read(); //AT+IPHONEACCEV=1,1,1 // 1 pair of key/value, key:1 (Battery Level), value: 0 - 9
                    write("OK\r");

                    stream.Close();

                    client.Close();

                    showBatteryLevel(msgFromJBL);
                }
            }
        }

        private string read(bool print = true)
        {
            string msg = null;

            if (stream.CanRead)
            {
                byte[] myReadBuffer = new byte[1024];
                StringBuilder myCompleteMessage = new StringBuilder();
                int numberOfBytesRead = 0;

                // Incoming message may be larger than the buffer size.
                do
                {
                    numberOfBytesRead = stream.Read(myReadBuffer, 0, myReadBuffer.Length);

                    myCompleteMessage.AppendFormat("{0}", Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead));

                }
                while (stream.DataAvailable);

                msg = myCompleteMessage.ToString().Substring(0, myCompleteMessage.Length - 1);
                // Print out the received message to the console.
                if (print)
                {
                    Console.WriteLine("AF (JBL E45BT) -> AG (Windows) : " + msg);
                }
            }
            else
            {
                Console.WriteLine("Sorry.  You cannot read from this NetworkStream.");
            }

            return msg;
        }

        private string write(string write, bool print = true)
        {
            string msg = null;

            if (stream.CanWrite)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(write);
                stream.Write(bytes, 0, bytes.Length);
                msg = write.Substring(0, write.Length - 1);
                if (print) {
                    Console.WriteLine("AG (Windows) -> AF (JBL E45BT) : " + msg);
                }
            }
            return msg;
        }

        private void showBatteryLevel(string atCommand)
        {
            var batteryLevel = atCommand.Substring(19);
            batterySlider.Value = Double.Parse(batteryLevel);
            ni.Text = "Battery: " + batteryLevel;
            ni.BalloonTipText = "Battery: " + batteryLevel;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
                this.Hide();

            base.OnStateChanged(e);
        }
    }
}
