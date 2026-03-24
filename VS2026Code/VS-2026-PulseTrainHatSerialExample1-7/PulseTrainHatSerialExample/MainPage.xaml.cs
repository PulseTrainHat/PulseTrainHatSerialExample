using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using System.Threading;
using System.Threading.Tasks;


// Test program for Pulse Train Hat http://www.pthat.com

namespace PulseTrainHatSerialExample
{
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// Private variables
        /// </summary>
        private SerialDevice serialPort = null;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;

        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;

        
        public static class MyStaticValues
        {
            //Axis is set flag
            public static int Xset = 0;
            public static int Yset = 0;
            public static int Zset = 0;
            public static int Eset = 0;

            //Pause Recieved flag
            public static int pauseXaxis = 0;
            public static int pauseYaxis = 0;
            public static int pauseZaxis = 0;
            public static int pauseEaxis = 0;
            public static int pauseAllaxis = 0;

            //Conversion Variables
            public static double convertRPM;
            public static double convertSTEPS;
        }

            public MainPage()
        {
            this.InitializeComponent();

            //Update UI
            PauseX.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
            PauseY.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
            PauseZ.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
            PauseE.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
            PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);

            //Disable the 'Connect' button 
            comPortInput.IsEnabled = false;
            
            //Call disable all method
            Disableall();



            listOfDevices = new ObservableCollection<DeviceInformation>();
            ListAvailablePorts();
        }

        /// <summary>
        /// ListAvailablePorts
        /// - Use SerialDevice.GetDeviceSelector to enumerate all serial devices
        /// - Attaches the DeviceInformation to the ListBox source so that DeviceIds are displayed
        /// </summary>
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                status.Text = "Select a device and connect";

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                }

                DeviceListSource.Source = listOfDevices;
                comPortInput.IsEnabled = true;
                ConnectDevices.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }

        /// <summary>
        /// comPortInput_Click: Action to take when 'Connect' button is clicked
        /// - Get the selected device index and use Id to create the SerialDevice object
        /// - Configure default settings for the serial port
        /// - Create the ReadCancellationTokenSource token
        /// - Start listening on the serial port input
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void comPortInput_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count <= 0)
            {
                status.Text = "Select a device and connect";
                return;
            }

            DeviceInformation entry = (DeviceInformation)selection[0];

            try
            {
                serialPort = await SerialDevice.FromIdAsync(entry.Id);

                // Disable the 'Connect' button 
                comPortInput.IsEnabled = false;

                // Configure serial settings
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(30);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(30);
                if (LowSpeedBaud.IsChecked == true)
                {
                    serialPort.BaudRate = 115200;
                }
                else
                {
                    serialPort.BaudRate = 806400;
                }
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                // Display configured settings
                status.Text = "Serial port configured successfully: ";
                status.Text += serialPort.BaudRate + "-";
                status.Text += serialPort.DataBits + "-";
                status.Text += serialPort.Parity.ToString() + "-";
                status.Text += serialPort.StopBits;

                // Set the RcvdText field to invoke the TextChanged callback
                // The callback launches an async Read task to wait for data
                rcvdText.Text = "Waiting for data...";

                // Create cancellation token object to close I/O operations when closing the device
                ReadCancellationTokenSource = new CancellationTokenSource();

                // Enable 'WRITE' button to allow sending data
                Enableall();
                Listen();
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                comPortInput.IsEnabled = true;
                Disableall();

            }
        }

        /// <summary>
        /// sendTextButton_Click: Action to take when 'WRITE' button is clicked
        /// - Create a DataWriter object with the OutputStream of the SerialDevice
        /// - Create an async task that performs the write operation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void sendTextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync();
                }
                else
                {
                    status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                status.Text = "sendTextButton_Click: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream 
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync()
        {
            Task<UInt32> storeAsyncTask;

            if (sendText.Text.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(sendText.Text);

                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    status.Text = sendText.Text + ", ";
                    status.Text += "bytes written successfully!";
                }
                //sendText.Text = "";
            }
            else
            {
                status.Text = "Enter the text you want to write and then click on 'WRITE'";
            }
        }

        /// <summary>
        /// - Create a DataReader object
        /// - Create an async task to read from the SerialDevice InputStream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    // keep reading the serial input
                    while (true)
                    {
                        await ReadAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    status.Text = "Reading task was cancelled, closing device and cleaning up";
                    CloseDevice();
                }
                else
                {
                    status.Text = ex.Message;
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// ReadAsync: Task that waits on data and reads asynchronously from the serial device InputStream
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 1024;

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                
            
                    rcvdText.Text = dataReaderObject.ReadString(bytesRead);


                    string input = rcvdText.Text;


                //Check if received message can be divided by 7 as our return messages are 7 bytes long
                if (input.Length % 7 == 0)
                {

                        //*********
                        for (int i = 0; i < input.Length; i += 7)
                        {
                            string sub = input.Substring(i, 7);


                        


                            //Check if Start X command Received
                        if (sub == "RI00SX*")
                        {
                            //Disable start X button
                            StartX.IsEnabled = false;

                            //Enable stop and pause X buttons
                            StopX.IsEnabled = true;
                            PauseX.IsEnabled = true;

                            //Call enable/disable method
                            StartRecievedEnableDisable();
                        }

               

                        //Check if Start Y command Received
                        if (sub == "RI00SY*")
                        {
                            StartY.IsEnabled = false;
                            StopY.IsEnabled = true;
                            PauseY.IsEnabled = true;
                            StartRecievedEnableDisable();
                        }

                        //Check if Start Z command Received
                        if (sub == "RI00SZ*")
                        {
                            StartZ.IsEnabled = false;
                            StopZ.IsEnabled = true;
                            PauseZ.IsEnabled = true;
                            StartRecievedEnableDisable();
                        }

                        //Check if Start E command Received
                        if (sub == "RI00SE*")
                        {                                                       
                            StartE.IsEnabled = false;
                            StopE.IsEnabled = true;
                            PauseE.IsEnabled = true;
                            StartRecievedEnableDisable();
                        }


                        //Check if Start ALL command Received
                        if (sub == "RI00SA*")
                        {

                            //Check if X axis is set
                            if (MyStaticValues.Xset == 1)
                            {
                                //Disable start X button
                                StartX.IsEnabled = false;

                                //Enable stop and pause X buttons
                                StopX.IsEnabled = true;                        
                                PauseX.IsEnabled = true;
                            }

                            if (MyStaticValues.Yset == 1)
                            {
                                StartY.IsEnabled = false;
                                StopY.IsEnabled = true;
                                PauseY.IsEnabled = true;
                            }

                            if (MyStaticValues.Zset == 1)
                            {
                                StartZ.IsEnabled = false;
                                StopZ.IsEnabled = true;
                                PauseZ.IsEnabled = true;
                            }

                            if (MyStaticValues.Eset == 1)
                            {
                                StartE.IsEnabled = false;
                                StopE.IsEnabled = true;
                                PauseE.IsEnabled = true;
                            }

                            StartRecievedEnableDisable();
                        }



                        //Check if Pause X command Received
                        if (sub == "RI00PX*")
                        {
                            //Replace pause X button text
                            PauseX.Content = "Resume X";

                            //Change pause X button colour
                            PauseX.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);

                            //Pause X is active
                            MyStaticValues.pauseXaxis = 1;
                        }

                        //Check if Resume X command Received
                        if (sub == "CI00PX*")
                        {
                            //Replace pause X button text
                            PauseX.Content = "Pause X";

                            //Change pause X button colour
                            PauseX.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                            
                            //Pause X is inactive
                            MyStaticValues.pauseXaxis = 0;
                        }


                        //Check if Pause Y command Received
                        if (sub == "RI00PY*")
                        {
                            PauseY.Content = "Resume Y";
                            PauseY.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);
                            MyStaticValues.pauseYaxis = 1;
                        }

                        //Check if Resume Y command Received
                        if (sub == "CI00PY*")
                        {
                            PauseY.Content = "Pause Y";
                            PauseY.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                            MyStaticValues.pauseYaxis = 0;
                        }


                        //Check if Pause Z command Received
                        if (sub == "RI00PZ*")
                        {
                            PauseZ.Content = "Resume Z";
                            PauseZ.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);
                            MyStaticValues.pauseZaxis = 1;
                        }

                        //Check if Resume Z command Received
                        if (sub == "CI00PZ*")
                        {
                            PauseZ.Content = "Pause Z";
                            PauseZ.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                            MyStaticValues.pauseZaxis = 0;
                        }


                        //Check if Pause E command Received
                        if (sub == "RI00PE*")
                        {
                            PauseE.Content = "Resume E";
                            PauseE.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);
                            MyStaticValues.pauseEaxis = 1;
                        }

                        //Check if Resume E command Received
                        if (sub == "CI00PE*")
                        {
                            PauseE.Content = "Pause E";
                            PauseE.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                            MyStaticValues.pauseEaxis = 0;
                        }


                        //Check if Pause ALL command Received
                        if (sub == "RI00PA*")
                        {
                            //Replace pause All button text
                            PauseAll.Content = "Resume ALL";

                            //Change pause All button colour
                            PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);

                            //Pause All is active
                            MyStaticValues.pauseAllaxis = 1;

                            //Checks if X axis has been set
                            if (MyStaticValues.Xset == 1)
                            {
                                //Replace pause X button text
                                PauseX.Content = "Resume X";

                                //Change pause X button colour
                                PauseX.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);

                                //Pause X is active
                                MyStaticValues.pauseXaxis = 1;
                            }

                            if (MyStaticValues.Yset == 1)
                            {
                                PauseY.Content = "Resume Y";
                                PauseY.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);
                                MyStaticValues.pauseYaxis = 1;
                            }

                            if (MyStaticValues.Zset == 1)
                            {
                                PauseZ.Content = "Resume Z";
                                PauseZ.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);
                                MyStaticValues.pauseZaxis = 1;
                            }

                            if (MyStaticValues.Eset == 1)
                            {
                                PauseE.Content = "Resume E";
                                PauseE.Background = new SolidColorBrush(Windows.UI.Colors.PaleGreen);
                                MyStaticValues.pauseEaxis = 1;
                            }

                        }

                        //Check if Resume All command Received
                        if (sub == "CI00PA*")
                        {
                            //Replace pause All button text
                            PauseAll.Content = "Pause All";

                            //Change pause All button colour
                            PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);

                            //Pause All is inactive
                            MyStaticValues.pauseAllaxis = 0;

                            //Checks if X axis is paused
                            if (MyStaticValues.pauseXaxis == 1)
                            {
                                //Replace pause X button text
                                PauseX.Content = "Pause X";

                                //Change pause X button colour
                                PauseX.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);

                                //Pause X is inactive
                                MyStaticValues.pauseXaxis = 0;
                            }

                            if (MyStaticValues.pauseYaxis == 1)
                            {
                                PauseY.Content = "Pause Y";
                                PauseY.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                MyStaticValues.pauseYaxis = 0;
                            }

                            if (MyStaticValues.pauseZaxis == 1)
                            {
                                PauseZ.Content = "Pause Z";
                                PauseZ.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                MyStaticValues.pauseZaxis = 0;
                            }

                            if (MyStaticValues.pauseEaxis == 1)
                            {
                                PauseE.Content = "Pause E";
                                PauseE.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                MyStaticValues.pauseEaxis = 0;
                            }

                        }


                        //Check if Set X Axis completed
                        if (sub == "CI00CX*")
                        {
                            //Enable start buttons
                            StartX.IsEnabled = true;                            
                            StartAll.IsEnabled = true;

                            //X axis set is active
                            MyStaticValues.Xset = 1;
                        }

                        //Check if Set Y Axis completed
                        if (sub == "CI00CY*")
                        {
                            StartY.IsEnabled = true;                           
                            StartAll.IsEnabled = true;
                            MyStaticValues.Yset = 1;
                        }


                        //Check if Set Z Axis completed
                        if (sub == "CI00CZ*")
                        {
                            StartZ.IsEnabled = true;
                            StartAll.IsEnabled = true;
                            MyStaticValues.Zset = 1;
                        }

                        //Check if Set E Axis completed
                        if (sub == "CI00CE*")
                        {
                            StartE.IsEnabled = true;
                            StartAll.IsEnabled = true;
                            MyStaticValues.Eset = 1;
                        }


                        //Check if X Axis Stop button pressed
                        if (sub == "RI00TX*")
                        {
                            //Disable stop and pause X buttons
                            StopX.IsEnabled = false;
                            PauseX.IsEnabled = false;

                            //Check if X axis is paused
                            if (MyStaticValues.pauseXaxis == 1)
                            {
                                //Replace pause X button text
                                PauseX.Content = "Pause X";

                                //Change pause X button colour
                                PauseX.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);

                                //Pause X is inactive
                                MyStaticValues.pauseXaxis = 0;
                            }

                            //X axis set is inactive
                            MyStaticValues.Xset = 0;
                        }


                        //Check if Y Axis Stop button pressed
                        if (sub == "RI00TY*")
                        {
                            StopY.IsEnabled = false;
                            PauseY.IsEnabled = false;
                            if (MyStaticValues.pauseYaxis == 1)
                            {
                                PauseY.Content = "Pause Y";
                                PauseY.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                MyStaticValues.pauseYaxis = 0;
                            }

                            MyStaticValues.Yset = 0;
                        }

                        //Check if X Axis Stop button pressed
                        if (sub == "RI00TZ*")
                        {
                            StopZ.IsEnabled = false;
                            PauseZ.IsEnabled = false;
                            if (MyStaticValues.pauseZaxis == 1)
                            {
                                PauseZ.Content = "Pause Z";
                                PauseZ.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                MyStaticValues.pauseZaxis = 0;
                            }

                            MyStaticValues.Zset = 0;
                        }


                        //Check if E Axis Stop button pressed
                        if (sub == "RI00TE*")
                        {
                            StopE.IsEnabled = false;
                            PauseE.IsEnabled = false;
                            if (MyStaticValues.pauseEaxis == 1)
                            {
                                PauseE.Content = "Pause E";
                                PauseE.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                MyStaticValues.pauseEaxis = 0;
                            }

                            MyStaticValues.Eset = 0;
                        }


                        //Check if ALL Axis Stop button pressed
                        if (sub == "RI00TA*")
                        {
                            StopAll.IsEnabled = false;
                            PauseAll.IsEnabled = false;
                            if (MyStaticValues.pauseAllaxis == 1)
                            {
                                PauseAll.Content = "Pause All";
                                PauseAll.Background = new SolidColorBrush(Windows.UI.Colors.LightBlue);
                                MyStaticValues.pauseAllaxis = 0;
                            }

                           
                        }

                        //Check if X Axis completed amount of pulses
                        if (sub == "CI00SX*")
                        {
                            //Disable stop and pause X buttons
                            StopX.IsEnabled = false;
                            PauseX.IsEnabled = false;

                            //X axis set is inactive
                            MyStaticValues.Xset = 0;
                        }
                      

                        //Check if Y Axis completed amount of pulses
                        if (sub == "CI00SY*")
                        {
                            StopY.IsEnabled = false;
                            PauseY.IsEnabled = false;
                            MyStaticValues.Yset = 0;
                        }

                        //Check if Z Axis completed amount of pulses
                        if (sub == "CI00SZ*")
                        {
                            StopZ.IsEnabled = false;
                            PauseZ.IsEnabled = false;                          
                            MyStaticValues.Zset = 0;
                        }

                        //Check if E Axis completed amount of pulses
                        if (sub == "CI00SE*")
                        {
                            StopE.IsEnabled = false;
                            PauseE.IsEnabled = false;                           
                            MyStaticValues.Eset = 0;
                        }

                        



                        //Check if all completed
                        int checkall = MyStaticValues.Xset + MyStaticValues.Yset + MyStaticValues.Zset + MyStaticValues.Eset;
                        if (checkall == 0)
                        {
                            StopAll.IsEnabled = false;
                            PauseAll.IsEnabled = false;
                            ToggleEnableLine.IsEnabled = true;

                        }




                    } // end of for loop

                    } //endof checking length if

        

                status.Text = "bytes read successfully!";
                
            } //End of checking for bytes
        } //end of async read

        /// <summary>
        /// CancelReadTask:
        /// - Uses the ReadCancellationTokenSource to cancel read operations
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// CloseDevice:
        /// - Disposes SerialDevice object
        /// - Clears the enumerated device Id list
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;

            comPortInput.IsEnabled = true;
            sendTextButton.IsEnabled = false;
            rcvdText.Text = "";
            listOfDevices.Clear();
        }

        /// <summary>
        /// closeDevice_Click: Action to take when 'Disconnect and Refresh List' is clicked on
        /// - Cancel all read operations
        /// - Close and dispose the SerialDevice object
        /// - Enumerate connected devices
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void closeDevice_Click(object sender, RoutedEventArgs e)
        {
            Disconnectserial();
        }


        private void Disconnectserial()
        {

            try
            {
                status.Text = "";
                CancelReadTask();
                CloseDevice();
                ListAvailablePorts();
                Disableall();

            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
            }
        }


       


        private async void SendDataOut()
        {

            try

            {
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync();

                }
                else
                {
                    status.Text = "Select a device and connect";
                }
            }
            catch (Exception ex)
            {
                status.Text = "Send Data: " + ex.Message;
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }

        }
       

        //Alter all textboxes to the correct string format
        private void formatboxes()
        {

            XFreq.Text = String.Format("{0:000000.000}", Convert.ToDouble(XFreq.Text));
            Xpulsecount.Text = String.Format("{0:0000000000}", Convert.ToDouble(Xpulsecount.Text));
            Xdir.Text = String.Format("{0:0}", Convert.ToDouble(Xdir.Text));
            XRampUp.Text = String.Format("{0:0}", Convert.ToDouble(XRampUp.Text));
            XRampDown.Text = String.Format("{0:0}", Convert.ToDouble(XRampDown.Text));
            Xrampdivide.Text = String.Format("{0:000}", Convert.ToDouble(Xrampdivide.Text));
            Xramppause.Text = String.Format("{0:000}", Convert.ToDouble(Xramppause.Text));
            Xadc.Text = String.Format("{0:0}", Convert.ToDouble(Xadc.Text));
            EnableX.Text = String.Format("{0:0}", Convert.ToDouble(EnableX.Text));
            FormattedX.Text = "I00CX" + XFreq.Text + Xpulsecount.Text + Xdir.Text + XRampUp.Text + XRampDown.Text + Xrampdivide.Text + Xramppause.Text + Xadc.Text + EnableX.Text + "*";

            YFreq.Text = String.Format("{0:000000.000}", Convert.ToDouble(YFreq.Text));
            Ypulsecount.Text = String.Format("{0:0000000000}", Convert.ToDouble(Ypulsecount.Text));
            Ydir.Text = String.Format("{0:0}", Convert.ToDouble(Ydir.Text));
            YRampUp.Text = String.Format("{0:0}", Convert.ToDouble(YRampUp.Text));
            YRampDown.Text = String.Format("{0:0}", Convert.ToDouble(YRampDown.Text));
            Yrampdivide.Text = String.Format("{0:000}", Convert.ToDouble(Yrampdivide.Text));
            Yramppause.Text = String.Format("{0:000}", Convert.ToDouble(Yramppause.Text));
            Yadc.Text = String.Format("{0:0}", Convert.ToDouble(Yadc.Text));
            EnableY.Text = String.Format("{0:0}", Convert.ToDouble(EnableY.Text));
            FormattedY.Text = "I00CY" + YFreq.Text + Ypulsecount.Text + Ydir.Text + YRampUp.Text + YRampDown.Text + Yrampdivide.Text + Yramppause.Text + Yadc.Text + EnableY.Text + "*";

            ZFreq.Text = String.Format("{0:000000.000}", Convert.ToDouble(ZFreq.Text));
            Zpulsecount.Text = String.Format("{0:0000000000}", Convert.ToDouble(Zpulsecount.Text));
            Zdir.Text = String.Format("{0:0}", Convert.ToDouble(Zdir.Text));
            ZRampUp.Text = String.Format("{0:0}", Convert.ToDouble(ZRampUp.Text));
            ZRampDown.Text = String.Format("{0:0}", Convert.ToDouble(ZRampDown.Text));
            Zrampdivide.Text = String.Format("{0:000}", Convert.ToDouble(Zrampdivide.Text));
            Zramppause.Text = String.Format("{0:000}", Convert.ToDouble(Zramppause.Text));
            Zadc.Text = String.Format("{0:0}", Convert.ToDouble(Zadc.Text));
            EnableZ.Text = String.Format("{0:0}", Convert.ToDouble(EnableZ.Text));
            FormattedZ.Text = "I00CZ" + ZFreq.Text + Zpulsecount.Text + Zdir.Text + ZRampUp.Text + ZRampDown.Text + Zrampdivide.Text + Zramppause.Text + Zadc.Text + EnableZ.Text + "*";

            EFreq.Text = String.Format("{0:000000.000}", Convert.ToDouble(EFreq.Text));
            Epulsecount.Text = String.Format("{0:0000000000}", Convert.ToDouble(Epulsecount.Text));
            Edir.Text = String.Format("{0:0}", Convert.ToDouble(Edir.Text));
            ERampUp.Text = String.Format("{0:0}", Convert.ToDouble(ERampUp.Text));
            ERampDown.Text = String.Format("{0:0}", Convert.ToDouble(ERampDown.Text));
            Erampdivide.Text = String.Format("{0:000}", Convert.ToDouble(Erampdivide.Text));
            Eramppause.Text = String.Format("{0:000}", Convert.ToDouble(Eramppause.Text));
            Eadc.Text = String.Format("{0:0}", Convert.ToDouble(Eadc.Text));
            EnableE.Text = String.Format("{0:0}", Convert.ToDouble(EnableE.Text));
            FormattedE.Text = "I00CE" + EFreq.Text + Epulsecount.Text + Edir.Text + ERampUp.Text + ERampDown.Text + Erampdivide.Text + Eramppause.Text + Eadc.Text + EnableE.Text + "*";


        }

   
        private  void Firmware_Click(object sender, RoutedEventArgs e)
        {
            //Requests the Firmware Version from the PTHAT
            sendText.Text = "I00FW*";
            SendDataOut();
        }

        private void SetX_Click(object sender, RoutedEventArgs e)
        {
            //Calls format box method
            formatboxes();

            //Sends a Set X Axis command
            sendText.Text = FormattedX.Text;
            SendDataOut();
        }
                
        private void SetY_Click(object sender, RoutedEventArgs e)
        {
            formatboxes();
            sendText.Text = FormattedY.Text;
            SendDataOut();
        }

        private void SetZ_Click(object sender, RoutedEventArgs e)
        {
            formatboxes();
            sendText.Text = FormattedZ.Text;
            SendDataOut();
        }

        private void SetE_Click(object sender, RoutedEventArgs e)
        {
            formatboxes();
            sendText.Text = FormattedE.Text;
            SendDataOut();
        }

        private void StartAll_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Start All command
            sendText.Text = "I00SA*";
            SendDataOut();
        }

        private void StopAll_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Stop All command
            sendText.Text = "I00TA*";
            SendDataOut();            
        }

        private void GetXPulses_Click(object sender, RoutedEventArgs e)
        {
            //Requests the Current X Axis Pulse Count
            sendText.Text = "I00XP*";
            SendDataOut();
        }

        private void GetYPulses_Click(object sender, RoutedEventArgs e)
        {
           sendText.Text = "I00YP*";
           SendDataOut();
        }

        private void GetZPulses_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00ZP*";
            SendDataOut();
        }

        private void GetEPulses_Click(object sender, RoutedEventArgs e)
        { 
            sendText.Text = "I00EP*";
            SendDataOut();
        }

        private void Calcit_Click(object sender, RoutedEventArgs e)
        {
            //Call format boxes method
            formatboxes();
        }


        private void Aux1On_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Switch On Aux1 Command
            sendText.Text = "I00A11*";
            SendDataOut();            
        }

        private void Aux1Off_Click(object sender, RoutedEventArgs e)
        {
           //Sends a Switch Off Aux1 Command
           sendText.Text = "I00A10*";
           SendDataOut();
        }

        private void Aux2On_Click(object sender, RoutedEventArgs e)
        {
           sendText.Text = "I00A21*";
           SendDataOut();
        }

        private void Aux2Off_Click(object sender, RoutedEventArgs e)
        {
           sendText.Text = "I00A20*";
           SendDataOut();
        }

        private void Aux3On_Click(object sender, RoutedEventArgs e)
        { 
           sendText.Text = "I00A31*";
           SendDataOut();
        }

        private void Aux3Off_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00A30*";
            SendDataOut();
        }

        private void GetADC1_Click(object sender, RoutedEventArgs e)
        {
            //Requests current ADC1 Reading
            sendText.Text = "I00D1*";
            SendDataOut();
        }

        private void GetADC2_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00D2*";
            SendDataOut();
        }

        private void StartX_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Start X Axis Command
            sendText.Text = "I00SX*";
            SendDataOut();
        }

        private void StartY_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00SY*";
            SendDataOut();            
        }

        private void StartZ_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00SZ*";
            SendDataOut();            
        }

        private void StartE_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00SE*";
            SendDataOut();            
        }

        private void StopX_Click(object sender, RoutedEventArgs e)
        {
            //Calls Check if any Axis is set Method
            Checkall();

            //Sends a Stop X Axis Command
            sendText.Text = "I00TX*";
            SendDataOut();
        }

        private void StopY_Click(object sender, RoutedEventArgs e)
        {
            Checkall();
            sendText.Text = "I00TY*";
            SendDataOut();            
        }

        private void StopZ_Click(object sender, RoutedEventArgs e)
        {
            Checkall();
            sendText.Text = "I00TZ*";
            SendDataOut();
        }

        private void StopE_Click(object sender, RoutedEventArgs e)
        {
            Checkall();
            sendText.Text = "I00TE*";
            SendDataOut();
        }

        private void TurnOnReceivedReplies_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Switch On Recieved Replies Command
            sendText.Text = "I00R1*";
            SendDataOut();
        }

        private void TurnOffReceivedReplies_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Switch Off Recieved Replies Command
            sendText.Text = "I00R0*";
            SendDataOut();
        }

        private void TurnOnCompletedReplies_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Switch On Completed Replies Command
            sendText.Text = "I00G1*";
            SendDataOut();
        }

        private void TurnOffCompletedReplies_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Switch Off Completed Replies Command
            sendText.Text = "I00G0*";
            SendDataOut();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Reset Command
            sendText.Text = "N*";
            SendDataOut();
        }

        private void PauseX_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Pause X Axis Command
            sendText.Text = "I00PX0000*";
            SendDataOut();
        }

        private void PauseY_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00PY0000*";
            SendDataOut();
        }

        private void PauseZ_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00PZ0000*";
            SendDataOut();
        }

        private void PauseE_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00PE0000*";
            SendDataOut();
        }

        private void PauseAll_Click(object sender, RoutedEventArgs e)
        {
            sendText.Text = "I00PA0000*";
            SendDataOut();
        }

        private void ToggleEnableLine_Click(object sender, RoutedEventArgs e)
        {
            //Sends a Toggle Enable Line Command
            sendText.Text = "I00HT*";
            SendDataOut();
        }

        private void RPM_TextChanged(object sender, TextChangedEventArgs e)
        {
           Conversions();
        }

        private void StepsPerRev_TextChanged(object sender, TextChangedEventArgs e)
        {
          Conversions();
        }

		private void Conversions()
		{
			 //Check if Steps per revolution Textbox is not Null or empty
            if (!String.IsNullOrEmpty(StepsPerRev.Text.Trim()))
            {
                //Convert our Steps per revolution into a Frequency
                MyStaticValues.convertSTEPS = Convert.ToDouble(StepsPerRev.Text) * 0.0166666666666667;
            }

            //Check if RPM Textbox is not Null or empty
            if (!String.IsNullOrEmpty(RPM.Text.Trim()))
            {
                //Multiply the RPM by our new frequency
                MyStaticValues.convertRPM = Convert.ToDouble(RPM.Text) * MyStaticValues.convertSTEPS;

                //Convert our value to match the DDS Resolution
                MyStaticValues.convertRPM = (Math.Round(MyStaticValues.convertRPM / 0.004)) * 0.004;
            }

            //Format string
            HZresult.Text = Convert.ToString(MyStaticValues.convertRPM);
            HZresult.Text = String.Format("{0:000000.000}", Convert.ToDouble(HZresult.Text));
		}
        
        private void EnableX_TextChanged(object sender, TextChangedEventArgs e)
        {
            //Call EnableChange method with Axis that's been changed
            EnableLineUpdate(EnableX);
        }

        private void EnableY_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableLineUpdate(EnableY);
        }

        private void EnableZ_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableLineUpdate(EnableZ);
        }

        private void EnableE_TextChanged(object sender, TextChangedEventArgs e)
        {
            EnableLineUpdate(EnableE);
        }

        //Updates all Enable Line textboxes
        private void EnableLineUpdate(TextBox a)
        {
            EnableX.Text = a.Text;
            EnableY.Text = a.Text;
            EnableZ.Text = a.Text;
            EnableE.Text = a.Text;
        }


        private void Disableall()
        {
            sendTextButton.IsEnabled = false;
            Firmware1.IsEnabled = false;

            SetX.IsEnabled = false;
            SetY.IsEnabled = false;
            SetZ.IsEnabled = false;
            SetE.IsEnabled = false;

            StartX.IsEnabled = false;
            StartY.IsEnabled = false;
            StartZ.IsEnabled = false;
            StartE.IsEnabled = false;
            StartAll.IsEnabled = false;

            PauseX.IsEnabled = false;
            PauseY.IsEnabled = false;
            PauseZ.IsEnabled = false;
            PauseE.IsEnabled = false;
            PauseAll.IsEnabled = false;

            StopX.IsEnabled = false;
            StopY.IsEnabled = false;
            StopZ.IsEnabled = false;
            StopE.IsEnabled = false;
            StopAll.IsEnabled = false;

            GetXPulses.IsEnabled = false;
            GetYPulses.IsEnabled = false;
            GetZPulses.IsEnabled = false;
            GetEPulses.IsEnabled = false;
            GetADC1.IsEnabled = false;
            GetADC2.IsEnabled = false;

            Aux1On.IsEnabled = false;
            Aux2On.IsEnabled = false;
            Aux3On.IsEnabled = false;
            Aux1Off.IsEnabled = false;
            Aux2Off.IsEnabled = false;
            Aux3Off.IsEnabled = false;

            TurnOnReceivedReplies.IsEnabled = false;
            TurnOffReceivedReplies.IsEnabled = false;
            TurnOnCompletedReplies.IsEnabled = false;
            TurnOffCompletedReplies.IsEnabled = false;

            Reset.IsEnabled = false;
            ToggleEnableLine.IsEnabled = false;
        }

        private void Enableall()
        {
            sendTextButton.IsEnabled = true;
            Firmware1.IsEnabled = true;

            SetX.IsEnabled = true;
            SetY.IsEnabled = true;
            SetZ.IsEnabled = true;
            SetE.IsEnabled = true;
            Reset.IsEnabled = true;
            ToggleEnableLine.IsEnabled = true;
            sendText.Text = "";


            GetXPulses.IsEnabled = true;
            GetYPulses.IsEnabled = true;
            GetZPulses.IsEnabled = true;
            GetEPulses.IsEnabled = true;
            GetADC1.IsEnabled = true;
            GetADC2.IsEnabled = true;

            Aux1On.IsEnabled = true;
            Aux2On.IsEnabled = true;
            Aux3On.IsEnabled = true;
            Aux1Off.IsEnabled = true;
            Aux2Off.IsEnabled = true;
            Aux3Off.IsEnabled = true;

            TurnOnReceivedReplies.IsEnabled = true;
            TurnOffReceivedReplies.IsEnabled = true;
            TurnOnCompletedReplies.IsEnabled = true;
            TurnOffCompletedReplies.IsEnabled = true;
        }


        public void Checkall ()
        {
            int checkall = MyStaticValues.Xset + MyStaticValues.Yset + MyStaticValues.Zset + MyStaticValues.Eset;
            if (checkall == 0)
            {
                StopAll.IsEnabled = false;
                PauseAll.IsEnabled = false;
            }
        }

        public void StartRecievedEnableDisable()
        {
            PauseAll.IsEnabled = true;
            StartAll.IsEnabled = false;
            StopAll.IsEnabled = true;
            ToggleEnableLine.IsEnabled = false;
        }

    }
}
