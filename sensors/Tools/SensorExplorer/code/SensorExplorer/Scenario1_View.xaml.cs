﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Devices.SerialCommunication;
using Windows.Foundation;
using Windows.Foundation.Diagnostics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SensorExplorer
{
    public sealed partial class Scenario1View : Page
    {
        public static Scenario1View Scenario1;

        private List<SensorData> sensorData;
        private List<SensorDisplay> sensorDisplay;
        private MainPage rootPage = MainPage.Current;
        private Popup settingsPopup; // This is the container that will hold our custom content

        // MALT
        private const string buttonNameDisconnectFromDevice = "Disconnect from device";
        private const string buttonNameDisableReconnectToDevice = "Do not automatically reconnect to device that was just closed";

        private bool cancel = false;
        private bool isAllDevicesEnumerated;
        private bool watchersStarted;
        private bool watchersSuspended;
        private DataReader DataReaderObject = null;
        private DataWriter DataWriterObject = null;
        private Dictionary<DeviceWatcher, string> mapDeviceWatchersToDeviceSelector;
        private double windowHeight;
        private double windowWidth;
        private EventHandler<object> appResumeEventHandler;
        private List<string> conversionValues = new List<string> { "100", "800" };
        private ObservableCollection<DeviceListEntry> listOfDevices = new ObservableCollection<DeviceListEntry>();
        private SuspendingEventHandler appSuspendEventHandler;

        public Scenario1View()
        {
            InitializeComponent();
            Scenario1 = this;

            SizeChanged += MainPageSizeChanged;

            sensorDisplay = new List<SensorDisplay>();
            sensorData = new List<SensorData>();

            EnumerateSensors();

            PeriodicTimer.SensorData = sensorData;
            PeriodicTimer.SensorDisplay = sensorDisplay;

            Sensor.SensorData = sensorData;
            Sensor.SensorDisplay = sensorDisplay;

            var resourceLoader = ResourceLoader.GetForCurrentView();

            saveFileButton.Click += SaveFileButtonClick;

            rootPage.NotifyUser("Enumerating sensors...", NotifyType.StatusMessage);

            // For MALT
            mapDeviceWatchersToDeviceSelector = new Dictionary<DeviceWatcher, string>();
            watchersStarted = false;
            watchersSuspended = false;
            isAllDevicesEnumerated = false;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// Create the DeviceWatcher objects when the user navigates to this page so the UI list of devices is populated.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs eventArgs)
        {            
            // If we are connected to the device or planning to reconnect, we should disable the list of devices
            // to prevent the user from opening a device without explicitly closing or disabling the auto reconnect
            if (EventHandlerForDevice.Current.IsDeviceConnected
                || (EventHandlerForDevice.Current.IsEnabledAutoReconnect
                && EventHandlerForDevice.Current.DeviceInformation != null))
            {
                UpdateConnectDisconnectButtonsAndList(false);

                // These notifications will occur if we are waiting to reconnect to device when we start the page
                EventHandlerForDevice.Current.OnDeviceConnected = OnDeviceConnected;
            }
            else
            {
                UpdateConnectDisconnectButtonsAndList(true);
            }

            // Begin watching out for events
            StartHandlingAppEvents();

            // Initialize the desired device watchers so that we can watch for when devices are connected/removed
            InitializeDeviceWatchers();
            StartDeviceWatchers();

            DeviceListSource.Source = listOfDevices;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (Sensor.SensorDisplay != null && Sensor.SensorDisplay.Count > 0 
                && Sensor.CurrentId >= 0 && Sensor.CurrentId != PivotSensor.Items.Count - 1)
            {
                if (Sensor.SensorDisplay[Sensor.CurrentId].SensorType == Sensor.LIGHTSENSOR)
                {
                    DisconnectFromDeviceClick(null, null);
                }

                Sensor.DisableSensor(Sensor.SensorDisplay[Sensor.CurrentId].SensorType, Sensor.SensorDisplay[Sensor.CurrentId].Index);
            }

            rootPage.NotifyUser("", NotifyType.StatusMessage);
            StopHandlingAppEvents();
        }

        private async void EnumerateSensors()
        {
            try
            {
                rootPage.DisableScenarioSelect();
                await Sensor.GetDefault(true);
                rootPage.EnableScenarioSelect();

                int totalIndex = -1;
                for (int index = 0; index < Sensor.AccelerometerStandardList.Count; index++)
                {
                    totalIndex++;
                    SensorData _accelerometerData = new SensorData(Sensor.ACCELEROMETER, totalIndex, "Accelerometer (Standard)", new string[] { "AccelerationX (g)", "AccelerationY (g)", "AccelerationZ (g)" });
                    SensorDisplay _accelerometerDisplay = new SensorDisplay(Sensor.ACCELEROMETER, index, totalIndex, "Accelerometer (Standard)", -2, 2, 2, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan });
                    sensorDisplay.Add(_accelerometerDisplay);
                    sensorData.Add(_accelerometerData);
                    AddPivotItem(Sensor.ACCELEROMETER, index, totalIndex);
                }
                for (int index = 0; index < Sensor.AccelerometerLinearList.Count; index++)
                {
                    totalIndex++;
                    SensorData _accelerometerLinearData = new SensorData(Sensor.ACCELEROMETERLINEAR, totalIndex, "Accelerometer (Linear)", new string[] { "AccelerationX (g)", "AccelerationY (g)", "AccelerationZ (g)" });
                    SensorDisplay _accelerometerLinearDisplay = new SensorDisplay(Sensor.ACCELEROMETERLINEAR, index, totalIndex, "Accelerometer (Linear)", -2, 2, 2, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan });
                    sensorDisplay.Add(_accelerometerLinearDisplay);
                    sensorData.Add(_accelerometerLinearData);
                    AddPivotItem(Sensor.ACCELEROMETERLINEAR, index, totalIndex);
                }
                for (int index = 0; index < Sensor.AccelerometerGravityList.Count; index++)
                {
                    totalIndex++;
                    SensorData _accelerometerGravityData = new SensorData(Sensor.ACCELEROMETERGRAVITY, totalIndex, "Accelerometer (Gravity)", new string[] { "AccelerationX (g)", "AccelerationY (g)", "AccelerationZ (g)" });
                    SensorDisplay _accelerometerGravityDisplay = new SensorDisplay(Sensor.ACCELEROMETERGRAVITY, index, totalIndex, "Accelerometer (Gravity)", -2, 2, 2, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan });
                    sensorDisplay.Add(_accelerometerGravityDisplay);
                    sensorData.Add(_accelerometerGravityData);
                    AddPivotItem(Sensor.ACCELEROMETERGRAVITY, index, totalIndex);
                }
                for (int index = 0; index < Sensor.ActivitySensorList.Count; index++)
                {
                    totalIndex++;
                    SensorData _activitySensorData = new SensorData(Sensor.ACTIVITYSENSOR, totalIndex, "ActivitySensor", new string[] { "AccelerationX (g)", "AccelerationY (g)", "AccelerationZ (g)" });
                    SensorDisplay _activitySensorDisplay = new SensorDisplay(Sensor.ACTIVITYSENSOR, index, totalIndex, "ActivitySensor", 2, 0, 2, new Color[] { Colors.Gray, Colors.Brown, Colors.DarkRed, Colors.Orange, Colors.DarkOrange, Colors.Lime, Colors.DarkCyan, Colors.DarkViolet });
                    sensorDisplay.Add(_activitySensorDisplay);
                    sensorData.Add(_activitySensorData);
                    AddPivotItem(Sensor.ACTIVITYSENSOR, index, totalIndex);
                }
                if (Sensor.Altimeter != null)
                {
                    totalIndex++;
                    SensorData _altimeterData = new SensorData(Sensor.ALTIMETER, totalIndex, "Altimeter", new string[] { "AltitudeChange (m)" });
                    SensorDisplay _altimeterDisplay = new SensorDisplay(Sensor.ALTIMETER, 0, totalIndex, "Altimeter", -10, 10, 2, new Color[] { Colors.DarkRed });
                    sensorDisplay.Add(_altimeterDisplay);
                    sensorData.Add(_altimeterData);
                    AddPivotItem(Sensor.ALTIMETER, 0, totalIndex);
                }
                for (int index = 0; index < Sensor.BarometerList.Count; index++)
                {
                    totalIndex++;
                    SensorData _barometerData = new SensorData(Sensor.BAROMETER, totalIndex, "Barometer", new string[] { "Pressure (hPa)" });
                    SensorDisplay _barometerDisplay = new SensorDisplay(Sensor.BAROMETER, index, totalIndex, "Barometer", 950, 1050, 2, new Color[] { Colors.Lime });
                    sensorDisplay.Add(_barometerDisplay);
                    sensorData.Add(_barometerData);
                    AddPivotItem(Sensor.BAROMETER, index, totalIndex);
                }
                for (int index = 0; index < Sensor.CompassList.Count; index++)
                {
                    totalIndex++;
                    SensorData _compassData = new SensorData(Sensor.COMPASS, totalIndex, "Compass", new string[] { "MagneticNorth (°)", "TrueNorth (°)", "HeadingAccuracy" });
                    SensorDisplay _compassDisplay = new SensorDisplay(Sensor.COMPASS, index, totalIndex, "Compass", 0, 360, 2, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan });
                    sensorDisplay.Add(_compassDisplay);
                    sensorData.Add(_compassData);
                    AddPivotItem(Sensor.COMPASS, index, totalIndex);
                }
                for (int index = 0; index < Sensor.CustomSensorList.Count; index++)
                {
                    totalIndex++;
                    SensorData _customSensorData = new SensorData(Sensor.CUSTOMSENSOR, totalIndex, "CustomSensor", new string[0]);
                    SensorDisplay _customSensorDisplay = new SensorDisplay(Sensor.CUSTOMSENSOR, index, totalIndex, "CustomSensor", 0, 360, 2, new Color[0]);
                    sensorDisplay.Add(_customSensorDisplay);
                    sensorData.Add(_customSensorData);
                    AddPivotItem(Sensor.CUSTOMSENSOR, index, totalIndex);
                }
                for (int index = 0; index < Sensor.GyrometerList.Count; index++)
                {
                    totalIndex++;
                    SensorData _gyrometerData = new SensorData(Sensor.GYROMETER, totalIndex, "Gyrometer", new string[] { "AngularVelocityX (°/s)", "AngularVelocityY (°/s)", "AngularVelocityZ (°/s)" });
                    SensorDisplay _gyrometerDisplay = new SensorDisplay(Sensor.GYROMETER, index, totalIndex, "Gyrometer", -200, 200, 2, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan });
                    sensorDisplay.Add(_gyrometerDisplay);
                    sensorData.Add(_gyrometerData);
                    AddPivotItem(Sensor.GYROMETER, index, totalIndex);
                }
                for (int index = 0; index < Sensor.InclinometerList.Count; index++)
                {
                    totalIndex++;
                    SensorData _inclinometerData = new SensorData(Sensor.INCLINOMETER, totalIndex, "Inclinometer", new string[] { "Pitch (°)", "Roll (°)", "Yaw (°)", "YawAccuracy" });
                    SensorDisplay _inclinometerDisplay = new SensorDisplay(Sensor.INCLINOMETER, index, totalIndex, "Inclinometer", -180, 360, 3, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan, Colors.Black });
                    sensorDisplay.Add(_inclinometerDisplay);
                    sensorData.Add(_inclinometerData);
                    AddPivotItem(Sensor.INCLINOMETER, index, totalIndex);
                }
                for (int index = 0; index < Sensor.LightSensorList.Count; index++)
                {
                    totalIndex++;
                    SensorData _lightSensorData = new SensorData(Sensor.LIGHTSENSOR, totalIndex, "LightSensor", new string[] { "Illuminance (lux)", "Chromaticity X", "Chromaticity Y" });
                    SensorDisplay _lightSensorDisplay = new SensorDisplay(Sensor.LIGHTSENSOR, index, totalIndex, "LightSensor", 0, 1000, 2, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan });
                    sensorDisplay.Add(_lightSensorDisplay);
                    sensorData.Add(_lightSensorData);
                    AddPivotItem(Sensor.LIGHTSENSOR, index, totalIndex);
                }
                for (int index = 0; index < Sensor.MagnetometerList.Count; index++)
                {
                    totalIndex++;
                    SensorData _magnetometerData = new SensorData(Sensor.MAGNETOMETER, totalIndex, "Magnetometer", new string[] { "MagneticFieldX (µT)", "MagneticFieldY (µT)", "MagneticFieldZ (µT)" });
                    SensorDisplay _magnetometerDisplay = new SensorDisplay(Sensor.MAGNETOMETER, index, totalIndex, "Magnetometer", -500, 500, 2, new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan });
                    sensorDisplay.Add(_magnetometerDisplay);
                    sensorData.Add(_magnetometerData);
                    AddPivotItem(Sensor.MAGNETOMETER, index, totalIndex);
                }
                for (int index = 0; index < Sensor.OrientationAbsoluteList.Count; index++)
                {
                    totalIndex++;
                    SensorData _orientationAbsoluteData = new SensorData(Sensor.ORIENTATIONSENSOR, totalIndex, "Orientation (Absolute)",
                                                                   new string[] { "QuaternionX", "QuaternionY", "QuaternionZ", "QuaternionW",
                                                                                  "RotationMatrixM11", "RotationMatrixM12", "RotationMatrixM13",
                                                                                  "RotationMatrixM21", "RotationMatrixM22", "RotationMatrixM23",
                                                                                  "RotationMatrixM31", "RotationMatrixM32", "RotationMatrixM33" });
                    SensorDisplay _orientationAbsoluteDisplay = new SensorDisplay(Sensor.ORIENTATIONSENSOR, index, totalIndex, "Orientation (Absolute)", -1, 1, 2,
                                                                            new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan, Colors.DarkViolet,
                                                                                          Colors.Black, Colors.Black, Colors.Black,
                                                                                          Colors.Black, Colors.Black, Colors.Black,
                                                                                          Colors.Black, Colors.Black, Colors.Black });
                    sensorDisplay.Add(_orientationAbsoluteDisplay);
                    sensorData.Add(_orientationAbsoluteData);
                    AddPivotItem(Sensor.ORIENTATIONSENSOR, index, totalIndex);
                }
                for (int index = 0; index < Sensor.OrientationRelativeList.Count; index++)
                {
                    totalIndex++;
                    SensorData _orientationRelativeData = new SensorData(Sensor.ORIENTATIONRELATIVE, totalIndex, "Orientation (Relative)",
                                                                   new string[] { "QuaternionX", "QuaternionY", "QuaternionZ", "QuaternionW",
                                                                                  "RotationMatrixM11", "RotationMatrixM12", "RotationMatrixM13",
                                                                                  "RotationMatrixM21", "RotationMatrixM22", "RotationMatrixM23",
                                                                                  "RotationMatrixM31", "RotationMatrixM32", "RotationMatrixM33" });
                    SensorDisplay _orientationRelativeDisplay = new SensorDisplay(Sensor.ORIENTATIONRELATIVE, index, totalIndex, "Orientation (Relative)", -1, 1, 2,
                                                                            new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan, Colors.DarkViolet,
                                                                                          Colors.Black, Colors.Black, Colors.Black,
                                                                                          Colors.Black, Colors.Black, Colors.Black,
                                                                                          Colors.Black, Colors.Black, Colors.Black });
                    sensorDisplay.Add(_orientationRelativeDisplay);
                    sensorData.Add(_orientationRelativeData);
                    AddPivotItem(Sensor.ORIENTATIONRELATIVE, index, totalIndex);
                }
                for (int index = 0; index < Sensor.OrientationGeomagneticList.Count; index++)
                {
                    totalIndex++;
                    SensorData _orientationGeomagneticData = new SensorData(Sensor.ORIENTATIONGEOMAGNETIC, totalIndex, "Orientation (Geomagnetic)",
                                                                   new string[] { "QuaternionX", "QuaternionY", "QuaternionZ", "QuaternionW",
                                                                                  "RotationMatrixM11", "RotationMatrixM12", "RotationMatrixM13",
                                                                                  "RotationMatrixM21", "RotationMatrixM22", "RotationMatrixM23",
                                                                                  "RotationMatrixM31", "RotationMatrixM32", "RotationMatrixM33" });
                    SensorDisplay _orientationGeomagneticDisplay = new SensorDisplay(Sensor.ORIENTATIONGEOMAGNETIC, index, totalIndex, "Orientation (Geomagnetic)", -1, 1, 2,
                                                                            new Color[] { Colors.DarkRed, Colors.DarkOrange, Colors.DarkCyan, Colors.DarkViolet,
                                                                                          Colors.Black, Colors.Black, Colors.Black,
                                                                                          Colors.Black, Colors.Black, Colors.Black,
                                                                                          Colors.Black, Colors.Black, Colors.Black });
                    sensorDisplay.Add(_orientationGeomagneticDisplay);
                    sensorData.Add(_orientationGeomagneticData);
                    AddPivotItem(Sensor.ORIENTATIONGEOMAGNETIC, index, totalIndex);
                }
                for (int index = 0; index < Sensor.PedometerList.Count; index++)
                {
                    totalIndex++;
                    SensorData _pedometerData = new SensorData(Sensor.PEDOMETER, totalIndex, "Pedometer", new string[] { "CumulativeSteps", "CumulativeStepsDuration (s)", "StepKind" });
                    SensorDisplay _pedometerDisplay = new SensorDisplay(Sensor.PEDOMETER, index, totalIndex, "Pedometer", 0, 50, 2, new Color[] { Colors.DarkCyan, Colors.Black, Colors.Black });
                    sensorDisplay.Add(_pedometerDisplay);
                    sensorData.Add(_pedometerData);
                    AddPivotItem(Sensor.PEDOMETER, index, totalIndex);
                }
                for (int index = 0; index < Sensor.ProximitySensorList.Count; index++)
                {
                    totalIndex++;
                    SensorData _proximitySensorData = new SensorData(Sensor.PROXIMITYSENSOR, totalIndex, "ProximitySensor", new string[] { "IsDetected", "Distance (mm)" });
                    SensorDisplay _proximitySensorDisplay = new SensorDisplay(Sensor.PROXIMITYSENSOR, index, totalIndex, "ProximitySensor", 0, 1, 1, new Color[] { Colors.DarkOrange, Colors.Black });
                    sensorDisplay.Add(_proximitySensorDisplay);
                    sensorData.Add(_proximitySensorData);
                    AddPivotItem(Sensor.PROXIMITYSENSOR, index, totalIndex);
                }
                for (int index = 0; index < Sensor.SimpleOrientationSensorList.Count; index++)
                {
                    totalIndex++;
                    SensorData simpleOrientationSensorData = new SensorData(Sensor.SIMPLEORIENTATIONSENSOR, totalIndex, "SimpleOrientationSensor", new string[] { "SimpleOrientation" });
                    SensorDisplay simpleOrientationSensorDisplay = new SensorDisplay(Sensor.SIMPLEORIENTATIONSENSOR, index, totalIndex, "SimpleOrientationSensor", 0, 5, 5, new Color[] { Colors.Lime });
                    sensorDisplay.Add(simpleOrientationSensorDisplay);
                    sensorData.Add(simpleOrientationSensorData);
                    AddPivotItem(Sensor.PROXIMITYSENSOR, index, totalIndex);
                }

                AddSummaryPage();

                var resourceLoader = ResourceLoader.GetForCurrentView();
                rootPage.NotifyUser(resourceLoader.GetString("NumberOfSensors") + ": " + (PivotSensor.Items.Count - 1) + "\nNumber of sensors failed to enumerate: " + Sensor.NumFailedEnumerations, NotifyType.StatusMessage);
                ProgressRingSensor.IsActive = false;

                if (PivotSensor.Items.Count > 1)    // 1 for Summary Page which always exists
                {
                    PivotSensor.SelectionChanged += PivotSensorSelectionChanged;
                    PivotSensor.SelectedIndex = 0;
                    PivotSensorSelectionChanged(null, null);
                }
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
            }

            ProgressRingSensor.IsActive = false;
        }

        private void AddPivotItem(int sensorType, int index, int totalIndex)
        {
            PivotItem PivotItemSensor = new PivotItem();
            ScrollViewer scrollViewerSensor = new ScrollViewer() { VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Visible };

            PivotItemSensor.Header = sensorData[totalIndex].Name + " " + (index + 1);
            scrollViewerSensor.Content = sensorDisplay[totalIndex].StackPanelSensor;
            PivotItemSensor.Content = scrollViewerSensor;
            PivotSensor.Items.Add(PivotItemSensor);
        }

        /// <summary>        /// This the event handler for the "Defaults" button added to the settings charm. This method
        /// is responsible for creating the Popup window will use as the container for our settings Flyout.
        /// The reason we use a Popup is that it gives us the "light dismiss" behavior that when a user clicks away 
        /// from our custom UI it just dismisses.  This is a principle in the Settings experience and you see the
        /// same behavior in other experiences like AppBar. 
        /// </summary>
        void onSettingsCommand(IUICommand command)
        {
            // Create a Popup window which will contain our flyout
            settingsPopup = new Popup();
            settingsPopup.IsLightDismissEnabled = true;
            settingsPopup.Height = Window.Current.Bounds.Height;

            // Add the proper animation for the panel
            settingsPopup.ChildTransitions = new TransitionCollection();
            settingsPopup.ChildTransitions.Add(new PaneThemeTransition());

            // Let's define the location of our Popup
            settingsPopup.SetValue(Canvas.TopProperty, 0);
            settingsPopup.IsOpen = true;
        }

        void MainPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width - 50;
            windowWidth = e.NewSize.Width;
            windowHeight = e.NewSize.Height;
            double actualWidth = 0;
            for (int i = 0; i < sensorDisplay.Count; i++)
            {
                actualWidth = sensorDisplay[i].SetWidth(e.NewSize.Width, e.NewSize.Height);
            }
        }
         
        private void PivotSensorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            for (int i = 0; i < PivotSensor.Items.Count; i++)
            {
                if (i != PivotSensor.SelectedIndex)
                {
                    (((PivotSensor.Items[i] as PivotItem).Content as ScrollViewer).Content as StackPanel).Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (Sensor.SensorDisplay != null && Sensor.SensorDisplay.Count > 0 &&
                        Sensor.CurrentId != -1 && Sensor.CurrentId != PivotSensor.Items.Count - 1) // disable previous sensor
                    {
                        ShowPlotButton(null, null);
                        if (Sensor.SensorDisplay[Sensor.CurrentId].SensorType == Sensor.LIGHTSENSOR)
                        {
                            DisconnectFromDeviceClick(null, null);
                        }

                        Sensor.DisableSensor(Sensor.SensorDisplay[Sensor.CurrentId].SensorType, Sensor.SensorDisplay[Sensor.CurrentId].Index);
                    }

                    Sensor.CurrentId = i;   // sensor being displayed
                    (((PivotSensor.Items[i] as PivotItem).Content as ScrollViewer).Content as StackPanel).Visibility = Visibility.Visible;

                    if (Sensor.SensorDisplay != null && Sensor.SensorDisplay.Count > 0 &&  i != PivotSensor.Items.Count - 1)
                    {
                        sensorDisplay[i].EnableSensor();
                        SensorDisplay selected = sensorDisplay[Sensor.CurrentId];
                        selected.StackPanelProperty.Visibility = Visibility.Visible;

                        if ((PivotSensor.Items[i] as PivotItem).Header.ToString().Contains("LightSensor"))
                        {
                            saveFileButton.IsEnabled = true;
                            selected.MALTButton.Visibility = Visibility.Visible;
                            selected.StackPanelMALTData.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            saveFileButton.IsEnabled = false;
                            stackPanelMALTConnection.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        public void LogDataLightSensor(LightSensorReading reading)
        {
            LoggingFields loggingFields = new LoggingFields();
            loggingFields.AddString("Timestamp", reading.Timestamp.ToString());
            loggingFields.AddDouble("IlluminanceInLux", reading.IlluminanceInLux);
            rootPage.LoggingChannelView.LogEvent("LightSensorData", loggingFields);
        }

        public void MALTButton(object sender, RoutedEventArgs e)
        {
            cancel = false;

            SensorDisplay selected = sensorDisplay[Sensor.CurrentId];

            selected.MALTButton.Visibility = Visibility.Collapsed;
            stackPanelMALTConnection.Visibility = Visibility.Visible;
            selected.StackPanelProperty.Visibility = Visibility.Collapsed;
        }

        public async void ConnectToDeviceClick(object sender, RoutedEventArgs eventArgs)
        {
            SensorDisplay selected = sensorDisplay[Sensor.CurrentId];

            var selection = connectDevices.SelectedItems;
            DeviceListEntry entry = null;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;

                if (entry != null)
                {
                    // Create an EventHandlerForDevice to watch for the device we are connecting to
                    EventHandlerForDevice.CreateNewEventHandlerForDevice();

                    // Get notified when the device was successfully connected to or about to be closed
                    EventHandlerForDevice.Current.OnDeviceConnected = OnDeviceConnected;

                    // It is important that the FromIdAsync call is made on the UI thread because the consent prompt, when present,
                    // can only be displayed on the UI thread. Since this method is invoked by the UI, we are already in the UI thread.
                    bool openSuccess = await EventHandlerForDevice.Current.OpenDeviceAsync(entry.DeviceInformation, entry.DeviceSelector);

                    // Disable connect button if we connected to the device
                    UpdateConnectDisconnectButtonsAndList(!openSuccess);

                    if (openSuccess)
                    {
                        stackPanelMALTConnection.Visibility = Visibility.Collapsed;
                        selected.StackPanelMALTData.Visibility = Visibility.Visible;
                        PeriodicTimer.CreateScenario1();
                    }
                }
            }
        }

        private async void FeedbackButton(object sender, RoutedEventArgs e)
        {
            var uri = new Uri(@"feedback-hub:?
                                referrer=SensorExplorer&
                                tabid=2&
                                contextid=73&
                                appid=Microsoft.SensorExplorer1_8wekyb3d8bbwe!App&
                                newFeedback=true");
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
            if (!success)
            {
                rootPage.NotifyUser("Cannot launch Feedback Hub", NotifyType.ErrorMessage);
            }
        }

        public async void SaveFileButtonClick(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("ETL", new List<string>() { ".etl" });
            savePicker.SuggestedFileName = "SensorExplorerLog";
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                CachedFileManager.DeferUpdates(file);
                StorageFile logFileGenerated = await rootPage.LoggingSessionView.CloseAndSaveToFileAsync();
                await logFileGenerated.CopyAndReplaceAsync(file);
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete)
                {
                    rootPage.NotifyUser("File " + file.Name + " was saved.", NotifyType.StatusMessage);
                }
                else if (status == FileUpdateStatus.CompleteAndRenamed)
                {
                    rootPage.NotifyUser("File " + file.Name + " was renamed and saved.", NotifyType.StatusMessage);
                }
                else
                {
                    rootPage.NotifyUser("File " + file.Name + " couldn't be saved.", NotifyType.ErrorMessage);
                }
            }
            else
            {
                rootPage.NotifyUser("Operation cancelled.", NotifyType.ErrorMessage);
            }
        }

        private void HidePlotButton(object sender, RoutedEventArgs e)
        {
            try
            {
                SensorDisplay selected = sensorDisplay[Sensor.CurrentId];
                selected.PlotCanvas.HideCanvas();
                selected.StackPanelTop.Visibility = Visibility.Collapsed;
                hidePlotButton.IsEnabled = false;
                showPlotButton.IsEnabled = true;
            }
            catch { }
        }

        private void ShowPlotButton(object sender, RoutedEventArgs e)
        {
            try
            {
                SensorDisplay selected = sensorDisplay[Sensor.CurrentId];
                selected.PlotCanvas.ShowCanvas();
                selected.StackPanelTop.Visibility = Visibility.Visible;
                hidePlotButton.IsEnabled = true;
                showPlotButton.IsEnabled = false;
            }
            catch { }
        }

        private void AddSummaryPage()
        {
            PivotItem PivotItemSensor = new PivotItem();
            ScrollViewer scrollViewerSensor = new ScrollViewer();
            scrollViewerSensor.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            scrollViewerSensor.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;

            StackPanel stackpanel = new StackPanel();
            stackpanel.Orientation = Orientation.Horizontal;
            StackPanel stackpanelProperty = new StackPanel();
            StackPanel stackpanelValue = new StackPanel();
            StackPanel stackpanelFailed = new StackPanel();
            TextBlock[] TextBlockProperties = new TextBlock[20];
            TextBlock[] TextBlockValues = new TextBlock[20];
            TextBlock[] TextBlockFailed = new TextBlock[20];
            for (int i = 0; i < TextBlockProperties.Length; i++)
            {
                TextBlockProperties[i] = new TextBlock();
                TextBlockProperties[i].FontSize = 20;
                stackpanelProperty.Children.Add(TextBlockProperties[i]);

                TextBlockValues[i] = new TextBlock();
                TextBlockValues[i].FontSize = 20;
                TextBlockValues[i].HorizontalAlignment = HorizontalAlignment.Center;
                stackpanelValue.Children.Add(TextBlockValues[i]);

                TextBlockFailed[i] = new TextBlock();
                TextBlockFailed[i].Margin = new Thickness(20, 0, 0, 0);
                TextBlockFailed[i].FontSize = 20;
                TextBlockFailed[i].HorizontalAlignment = HorizontalAlignment.Center;
                stackpanelFailed.Children.Add(TextBlockFailed[i]);
            }

            stackpanel.Children.Add(stackpanelProperty);
            stackpanel.Children.Add(stackpanelValue);
            stackpanel.Children.Add(stackpanelFailed);

            TextBlockProperties[0].Text = "Sensor Category";
            TextBlockProperties[0].FontWeight = FontWeights.Bold;
            TextBlockProperties[1].Text = "Accelerometer (Standard)";
            TextBlockProperties[2].Text = "Accelerometer (Gravity)";
            TextBlockProperties[3].Text = "Accelerometer (Relative)";
            TextBlockProperties[4].Text = "Activity Sensor";
            TextBlockProperties[5].Text = "Altimeter";
            TextBlockProperties[6].Text = "Barometer";
            TextBlockProperties[7].Text = "Compass";
            TextBlockProperties[8].Text = "Custom Sensor";
            TextBlockProperties[9].Text = "Gyrometer";
            TextBlockProperties[10].Text = "Inclinometer";
            TextBlockProperties[11].Text = "Light Sensor";
            TextBlockProperties[12].Text = "Magnetometer";
            TextBlockProperties[13].Text = "Orientation Sensor (Absolute)";
            TextBlockProperties[14].Text = "Orientation Sensor (Geomagnetic)";
            TextBlockProperties[15].Text = "Orientation Sensor (Relative)";
            TextBlockProperties[16].Text = "Pedometer";
            TextBlockProperties[17].Text = "Proximity Sensor";
            TextBlockProperties[18].Text = "Simple Orientation Sensor";
            TextBlockProperties[19].Text = "Other";

            TextBlockValues[0].Text = "Number of Sensor(s) Available";
            TextBlockValues[0].FontWeight = FontWeights.Bold;
            TextBlockValues[1].Text = Sensor.AccelerometerStandardList.Count.ToString();
            TextBlockValues[2].Text = Sensor.AccelerometerGravityList.Count.ToString();
            TextBlockValues[3].Text = Sensor.AccelerometerLinearList.Count.ToString();
            TextBlockValues[4].Text = Sensor.ActivitySensorList.Count.ToString();
            TextBlockValues[5].Text = (Sensor.Altimeter == null) ? ("0") : ("1");
            TextBlockValues[6].Text = Sensor.BarometerList.Count.ToString();
            TextBlockValues[7].Text = Sensor.CompassList.Count.ToString();
            TextBlockValues[8].Text = Sensor.CustomSensorList.Count.ToString();
            TextBlockValues[9].Text = Sensor.GyrometerList.Count.ToString();
            TextBlockValues[10].Text = Sensor.InclinometerList.Count.ToString();
            TextBlockValues[11].Text = Sensor.LightSensorList.Count.ToString();
            TextBlockValues[12].Text = Sensor.MagnetometerList.Count.ToString();
            TextBlockValues[13].Text = Sensor.OrientationAbsoluteList.Count.ToString();
            TextBlockValues[14].Text = Sensor.OrientationGeomagneticList.Count.ToString();
            TextBlockValues[15].Text = Sensor.OrientationRelativeList.Count.ToString();
            TextBlockValues[16].Text = Sensor.PedometerList.Count.ToString();
            TextBlockValues[17].Text = Sensor.ProximitySensorList.Count.ToString();
            TextBlockValues[18].Text = Sensor.SimpleOrientationSensorList.Count.ToString();
            TextBlockValues[19].Text = Sensor.SensorClassDevice.Count.ToString();

            TextBlockFailed[0].Text = "Any Failed Enumerations";
            TextBlockFailed[0].FontWeight = FontWeights.Bold;
            TextBlockFailed[1].Text = Sensor.AccelerometerStandardFailed ? "Yes" : "No";
            TextBlockFailed[2].Text = Sensor.AccelerometerGravityFailed ? "Yes" : "No";
            TextBlockFailed[3].Text = Sensor.AccelerometerLinearFailed ? "Yes" : "No";
            TextBlockFailed[4].Text = Sensor.ActivitySensorFailed ? "Yes" : "No";
            TextBlockFailed[5].Text = Sensor.AltimeterFailed ? "Yes" : "No";
            TextBlockFailed[6].Text = Sensor.BarometerFailed ? "Yes" : "No";
            TextBlockFailed[7].Text = Sensor.CompassFailed ? "Yes" : "No";
            TextBlockFailed[8].Text = Sensor.CustomSensorFailed ? "Yes" : "No";
            TextBlockFailed[9].Text = Sensor.GyrometerFailed ? "Yes" : "No";
            TextBlockFailed[10].Text = Sensor.InclinometerFailed ? "Yes" : "No";
            TextBlockFailed[11].Text = Sensor.LightSensorFailed ? "Yes" : "No";
            TextBlockFailed[12].Text = Sensor.MagnetometerFailed ? "Yes" : "No";
            TextBlockFailed[13].Text = Sensor.OrientationAbsoluteFailed ? "Yes" : "No";
            TextBlockFailed[14].Text = Sensor.OrientationGeomagneticFailed ? "Yes" : "No";
            TextBlockFailed[15].Text = Sensor.OrientationRelativeFailed ? "Yes" : "No";
            TextBlockFailed[16].Text = Sensor.PedometerFailed ? "Yes" : "No";
            TextBlockFailed[17].Text = Sensor.ProximitySensorFailed ? "Yes" : "No";
            TextBlockFailed[18].Text = Sensor.SimpleOrientationSensorFailed ? "Yes" : "No";
            TextBlockFailed[19].Text = Sensor.OtherSensorFailed ? "Yes" : "No";

            PivotItemSensor.Header = "Summary";
            scrollViewerSensor.Content = stackpanel;
            PivotItemSensor.Content = scrollViewerSensor;
            PivotSensor.Items.Add(PivotItemSensor);
        }

        public void ReportIntervalButton(object sender, RoutedEventArgs e)
        {
            try
            {
                SensorDisplay selected = sensorDisplay[Sensor.CurrentId];
                if (selected.SensorType == Sensor.ACCELEROMETER)
                {
                    Sensor.AccelerometerStandardList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.ACCELEROMETERLINEAR)
                {
                    Sensor.AccelerometerLinearList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.ACCELEROMETERGRAVITY)
                {
                    Sensor.AccelerometerGravityList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                // ActivitySensor doesn't have ReportInterval
                else if (selected.SensorType == Sensor.ALTIMETER)
                {
                    Sensor.Altimeter.ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.BAROMETER)
                {
                    Sensor.BarometerList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.COMPASS)
                {
                    Sensor.CompassList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.CUSTOMSENSOR)
                {
                    Sensor.CustomSensorList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.GYROMETER)
                {
                    Sensor.GyrometerList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.INCLINOMETER)
                {
                    Sensor.InclinometerList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.LIGHTSENSOR)
                {
                    Sensor.LightSensorList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.MAGNETOMETER)
                {
                    Sensor.MagnetometerList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.ORIENTATIONSENSOR)
                {
                    Sensor.OrientationAbsoluteList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.ORIENTATIONGEOMAGNETIC)
                {
                    Sensor.OrientationGeomagneticList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.ORIENTATIONRELATIVE)
                {
                    Sensor.OrientationRelativeList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                else if (selected.SensorType == Sensor.PEDOMETER)
                {
                    Sensor.PedometerList[selected.Index].ReportInterval = uint.Parse(selected.TextboxReportInterval.Text);
                    sensorData[Sensor.CurrentId].UpdateReportInterval(uint.Parse(selected.TextboxReportInterval.Text));
                }
                //ProximitySensor doesn't have ReportInterval
                //SimpleOrientationSensor doesn't have ReportInterval               
            }
            catch { }
        }

        // MALT
        /// <summary>
        /// Initialize device watchers to watch for the Serial Devices.
        /// GetDeviceSelector return an AQS string that can be passed directly into DeviceWatcher.createWatcher() or DeviceInformation.createFromIdAsync(). 
        /// In this sample, a DeviceWatcher will be used to watch for devices because we can detect surprise device removals.
        /// </summary>
        private void InitializeDeviceWatchers()
        {
            string deviceSelector = SerialDevice.GetDeviceSelectorFromUsbVidPid(ArduinoDevice.Vid, ArduinoDevice.Pid);
            var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);
            // Allow the EventHandlerForDevice to handle device watcher events that relates or effects our device (i.e. device removal, addition, app suspension/resume)
            AddDeviceWatcher(deviceWatcher, deviceSelector);
        }

        private void StartHandlingAppEvents()
        {
            appSuspendEventHandler = new SuspendingEventHandler(OnAppSuspension);
            appResumeEventHandler = new EventHandler<object>(OnAppResume);

            // This event is raised when the app is exited and when the app is suspended
            Application.Current.Suspending += appSuspendEventHandler;
            Application.Current.Resuming += appResumeEventHandler;
        }

        private void StopHandlingAppEvents()
        {
            // This event is raised when the app is exited and when the app is suspended
            Application.Current.Suspending -= appSuspendEventHandler;
            Application.Current.Resuming -= appResumeEventHandler;
        }

        /// <summary>
        /// Registers for Added, Removed, and Enumerated events on the provided deviceWatcher before adding it to an internal list.
        /// </summary>
        private void AddDeviceWatcher(DeviceWatcher deviceWatcher, string deviceSelector)
        {
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(OnDeviceAdded);
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(OnDeviceRemoved);
            mapDeviceWatchersToDeviceSelector.Add(deviceWatcher, deviceSelector);
        }

        /// <summary>
        /// Starts all device watchers including ones that have been individually stopped.
        /// </summary>
        private void StartDeviceWatchers()
        {
            // Start all device watchers
            watchersStarted = true;
            isAllDevicesEnumerated = false;

            foreach (DeviceWatcher deviceWatcher in mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status != DeviceWatcherStatus.Started) && (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Start();
                }
            }
        }

        /// <summary>
        /// Stops all device watchers.
        /// </summary>
        private void StopDeviceWatchers()
        {
            // Clear the list of devices so we don't have potentially disconnected devices around
            ClearDeviceEntries();

            watchersStarted = false;
        }

        /// <summary>
        /// Creates a DeviceListEntry for a device and adds it to the list of devices in the UI
        /// </summary>
        private void AddDeviceToList(DeviceInformation deviceInformation, string deviceSelector)
        {
            // search the device list for a device with a matching interface ID
            var match = FindDevice(deviceInformation.Id);

            // Add the device if it's new
            if (match == null)
            {
                // Create a new element for this device interface, and queue up the query of its device information
                match = new DeviceListEntry(deviceInformation, deviceSelector);

                // Add the new element to the end of the list of devices
                listOfDevices.Add(match);
            }
        }

        private void RemoveDeviceFromList(string deviceId)
        {
            // Removes the device entry from the interal list; therefore the UI
            var deviceEntry = FindDevice(deviceId);

            listOfDevices.Remove(deviceEntry);
        }

        private void ClearDeviceEntries()
        {
            if (listOfDevices != null)
            {
                listOfDevices.Clear();
            }
        }

        /// <summary>
        /// Searches through the existing list of devices for the first DeviceListEntry that has the specified device Id.
        /// </summary>
        private DeviceListEntry FindDevice(string deviceId)
        {
            if (deviceId != null)
            {
                foreach (DeviceListEntry entry in listOfDevices)
                {
                    if (entry.DeviceInformation.Id == deviceId)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// We must stop the DeviceWatchers because device watchers will continue to raise events even if
        /// the app is in suspension, which is not desired (drains battery). We resume the device watcher once the app resumes again.
        /// </summary>
        private void OnAppSuspension(object sender, SuspendingEventArgs args)
        {
            if (watchersStarted)
            {
                watchersSuspended = true;
                StopDeviceWatchers();
            }
            else
            {
                watchersSuspended = false;
            }
        }

        /// <summary>
        /// See OnAppSuspension for why we are starting the device watchers again
        /// </summary>
        private void OnAppResume(object sender, object args)
        {
            if (watchersSuspended)
            {
                watchersSuspended = false;
                StartDeviceWatchers();
            }
        }

        /// <summary>
        /// We will remove the device from the UI
        /// </summary>
        private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
            {
                rootPage.NotifyUser("Device removed - " + deviceInformationUpdate.Id, NotifyType.StatusMessage);
                RemoveDeviceFromList(deviceInformationUpdate.Id);
            }));
        }

        /// <summary>
        /// This function will add the device to the listOfDevices so that it shows up in the UI
        /// </summary>
        private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            await rootPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(() =>
            {
                rootPage.NotifyUser("Device added - " + deviceInformation.Id, NotifyType.StatusMessage);
                AddDeviceToList(deviceInformation, mapDeviceWatchersToDeviceSelector[sender]);
            }));
        }

        /// <summary>
        /// If all the devices have been enumerated, select the device in the list we connected to. Otherwise let the EnumerationComplete event
        /// from the device watcher handle the device selection
        /// </summary>
        private void OnDeviceConnected(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            SensorDisplay selected = sensorDisplay[Sensor.CurrentId];

            // Find and select our connected device
            if (isAllDevicesEnumerated)
            {
                SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);
            }

            if (EventHandlerForDevice.Current.Device.PortName != "")
            {
                rootPage.NotifyUser("Connected to - " + EventHandlerForDevice.Current.Device.PortName + " - " +
                                    EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
            }
            else
            {
                rootPage.NotifyUser("Connected to - " + EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
            }
        }

        /// <summary>
        /// Selects the item in the UI's listbox that corresponds to the provided device id. If there are no
        /// matches, we will deselect anything that is selected.
        /// </summary>
        private void SelectDeviceInList(string deviceIdToSelect)
        {
            // Don't select anything by default.
            connectDevices.SelectedIndex = -1;

            for (int deviceListIndex = 0; deviceListIndex < listOfDevices.Count; deviceListIndex++)
            {
                if (listOfDevices[deviceListIndex].DeviceInformation.Id == deviceIdToSelect)
                {
                    connectDevices.SelectedIndex = deviceListIndex;
                    break;
                }
            }
        }

        /// <summary>
        /// When ButtonConnectToDevice is disabled, ConnectDevices list will also be disabled.
        /// </summary>
        private void UpdateConnectDisconnectButtonsAndList(bool enableConnectButton)
        {
            ConnectToDeviceButton.IsEnabled = enableConnectButton;
            connectDevices.IsEnabled = ConnectToDeviceButton.IsEnabled;
        }

        public async void GetMALTData()
        {
            try
            {
                SensorDisplay selected = sensorDisplay[Sensor.CurrentId];

                await WriteCommandAsync("READALSSENSOR 1\n");
                double ambientLux = await ReadLightSensor("READALSSENSOR 1\n");
                selected.TextBlockMALTPropertyValue1[0].Text = ((int)ambientLux).ToString();

                await WriteCommandAsync("READCOLORSENSOR 1\n");
                string[] result = await ReadColorSensor("READCOLORSENSOR 1\n");
                if (result != null && result.Length == 5)
                {
                    selected.TextBlockMALTPropertyValue1[1].Text = result[1];
                    selected.TextBlockMALTPropertyValue1[2].Text = result[2];
                    selected.TextBlockMALTPropertyValue1[3].Text = result[3];
                    selected.TextBlockMALTPropertyValue1[4].Text = result[4];
                }

                await WriteCommandAsync("READALSSENSOR 2\n");
                double screenLux = await ReadLightSensor("READALSSENSOR 2\n");
                selected.TextBlockMALTPropertyValue2[0].Text = ((int)screenLux).ToString();

                await WriteCommandAsync("READCOLORSENSOR 2\n");
                string[] result2 = await ReadColorSensor("READCOLORSENSOR 2\n");
                if (result2 != null && result.Length == 5)
                {
                    selected.TextBlockMALTPropertyValue2[1].Text = result2[1];
                    selected.TextBlockMALTPropertyValue2[2].Text = result2[2];
                    selected.TextBlockMALTPropertyValue2[3].Text = result2[3];
                    selected.TextBlockMALTPropertyValue2[4].Text = result2[4];
                }
            }
            catch { }
        }

        private async Task WriteCommandAsync(string command)
        {
            if (EventHandlerForDevice.Current.IsDeviceConnected)
            {
                try
                {
                    DataWriterObject = new DataWriter(EventHandlerForDevice.Current.Device.OutputStream);
                    DataWriterObject.WriteString(command);
                    uint x = await DataWriterObject.StoreAsync().AsTask();
                    DataWriterObject.DetachStream();
                    DataWriterObject = null;
                }
                catch { }
            }
        }

        private async Task<string[]> ReadColorSensor(string command)
        {
            try
            {
                string data = await ReadLines(5);
                data = data.Replace("\n", "");
                string[] delim = new string[1] { "\r" };
                string[] split = data.Split(delim, StringSplitOptions.RemoveEmptyEntries);

                return split;
            }
            catch { return new string[] { }; }
        }

        private async Task<string> ReadLines(int numLines)
        {
            try
            {
                int newLines = 0;
                string data = string.Empty;

                DataReaderObject = new DataReader(EventHandlerForDevice.Current.Device.InputStream);
                while (newLines != numLines && !cancel)
                {
                    uint x = await DataReaderObject.LoadAsync(1);
                    string s = DataReaderObject.ReadString(1);
                    data += s;
                    if (s == "\n")
                    {
                        newLines++;
                    }
                }
                DataReaderObject.DetachStream();
                DataReaderObject = null;

                return data;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void HideMALTButton(object sender, RoutedEventArgs e)
        {
            DisconnectFromDeviceClick(null, null);

            SensorDisplay selected = sensorDisplay[Sensor.CurrentId];

            selected.StackPanelMALTData.Visibility = Visibility.Collapsed;
            selected.MALTButton.Visibility = Visibility.Visible;
            selected.StackPanelProperty.Visibility = Visibility.Visible;
        }

        public void DisconnectFromDeviceClick(object sender, RoutedEventArgs eventArgs)
        {
            cancel = true;

            PeriodicTimer.Cancel3();

            try
            {
                DataWriterObject.Dispose();
                DataReaderObject.Dispose();
            }
            catch { }

            var selection = connectDevices.SelectedItems;
            DeviceListEntry entry = null;

            // Prevent auto reconnect because we are voluntarily closing it
            // Re-enable the ConnectDevice list and ConnectToDevice button if the connected/opened device was removed.
            EventHandlerForDevice.Current.IsEnabledAutoReconnect = false;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                entry = (DeviceListEntry)obj;

                if (entry != null)
                {
                    EventHandlerForDevice.Current.CloseDevice();
                }
            }

            UpdateConnectDisconnectButtonsAndList(true);
        }

        private async Task<double> ReadLightSensor(string command)
        {
            try
            {
                string data = await ReadLines(3);
                data = data.Replace("\n", "");
                string[] delim = new string[1] { "\r" };
                string[] split = data.Split(delim, StringSplitOptions.RemoveEmptyEntries);

                return RawToLux(Convert.ToInt32(split[2]), Convert.ToInt32(split[1]));
            }
            catch { return -1; }
        }

        private double RawToLux(int result, int exponent)
        {
            // Formula to convert raw sensor output to lux is defined in the OPT 3001 spec.
            // If you are using a different part, this calculation will be different.
            double lsbSize = .01 * (Math.Pow(2, exponent));
            return lsbSize * result;
        }
    }
}