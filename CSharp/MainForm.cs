using System;
using System.Windows.Forms;
using Vintasoft.WinTwain;

namespace TwainExtendedImageInfoDemo
{
    public partial class MainForm : Form
    {

        #region Fields

        /// <summary>
        /// TWAIN device manager.
        /// </summary>
        DeviceManager _deviceManager;

        /// <summary>
        /// Current device.
        /// </summary>
        Device _currentDevice;

        #endregion



        #region Constructor

        public MainForm()
        {
            // register the evaluation license for VintaSoft TWAIN .NET SDK
            Vintasoft.Twain.TwainGlobalSettings.Register("REG_USER", "REG_EMAIL", "EXPIRATION_DATE", "REG_CODE");

            InitializeComponent();

            this.Text = String.Format("VintaSoft TWAIN Extended Image Info Demo v{0}", TwainGlobalSettings.ProductVersion);

            // create instance of the DeviceManager class
            _deviceManager = new DeviceManager(this, this.Handle);
        }

        #endregion



        #region Methods

        /// <summary>
        /// Application form is shown.
        /// </summary>
        private void MainForm_Shown(object sender, EventArgs e)
        {
            //
            string[] extendedImageInfoNames = Enum.GetNames(typeof(ExtendedImageInfoId));
            //
            for (int i = 0; i < extendedImageInfoNames.Length; i++)
                extendedImageInfoCheckedListBox.Items.Add(extendedImageInfoNames[i]);

            // select the standard extended image infos
            SelectStandardExtendedImageInfos();

            // open TWAIN device manager
            OpenDeviceManager();
        }


        /// <summary>
        /// Sets form's UI state.
        /// </summary>
        private void SetFormUiState(bool enabled)
        {
            acquireImageButton.Enabled = enabled;
        }


        /// <summary>
        /// Opens TWAIN device manager.
        /// </summary>
        private bool OpenDeviceManager()
        {
            SetFormUiState(false);

            try
            {
                // try to find the device manager 2.x
                _deviceManager.IsTwain2Compatible = true;
                // if TWAIN device manager 2.x is NOT available
                if (!_deviceManager.IsTwainAvailable)
                {
                    // try to find the device manager 1.x
                    _deviceManager.IsTwain2Compatible = false;
                    // if TWAIN device manager 1.x is NOT available
                    if (!_deviceManager.IsTwainAvailable)
                    {
                        MessageBox.Show("TWAIN device manager is not found.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                // show dialog with error message
                MessageBox.Show(GetFullExceptionMessage(ex), "TWAIN device manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // if 64-bit TWAIN2 device manager is used
            if (IntPtr.Size == 8 && _deviceManager.IsTwain2Compatible)
            {
                if (!InitTwain2DeviceManagerMode())
                {
                    MessageBox.Show("TWAIN device manager is not opened.");
                    return false;
                }
            }

            try
            {
                // open the device manager
                _deviceManager.Open();
            }
            catch (Exception ex)
            {
                // show dialog with error message
                MessageBox.Show(GetFullExceptionMessage(ex), "TWAIN device manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // if no devices are found in the system
            if (_deviceManager.Devices.Count == 0)
            {
                MessageBox.Show("No devices found.");
                return false;
            }

            SetFormUiState(true);
            return true;
        }

        /// <summary>
        /// Initializes the device manager mode.
        /// </summary>
        private bool InitTwain2DeviceManagerMode()
        {
            // create a form that allows to view and edit mode of 64-bit TWAIN2 device manager
            using (SelectDeviceManagerModeForm form = new SelectDeviceManagerModeForm())
            {
                // initialize form
                form.StartPosition = FormStartPosition.CenterParent;
                form.Owner = this;
                form.Use32BitDevices = _deviceManager.Are32BitDevicesUsed;

                // show dialog
                if (form.ShowDialog() == DialogResult.OK)
                {
                    // if device manager mode is changed
                    if (form.Use32BitDevices != _deviceManager.Are32BitDevicesUsed)
                    {
                        try
                        {
                            // if 32-bit devices must be used
                            if (form.Use32BitDevices)
                                _deviceManager.Use32BitDevices();
                            else
                                _deviceManager.Use64BitDevices();
                        }
                        catch (TwainDeviceManagerException ex)
                        {
                            // show dialog with error message
                            MessageBox.Show(GetFullExceptionMessage(ex), "TWAIN device manager", MessageBoxButtons.OK, MessageBoxIcon.Error);

                            return false;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Acquires image.
        /// </summary>
        private void acquireImageButton_Click(object sender, EventArgs e)
        {
            acquireImageButton.Enabled = false;
            try
            {
                // select the default device
                if (!_deviceManager.ShowDefaultDeviceSelectionDialog())
                {
                    MessageBox.Show("Device is not selected.");
                    acquireImageButton.Enabled = true;
                    return;
                }

                if (_currentDevice != null)
                    UnsubscribeFromDeviceEvents();

                // get reference to the selected device
                Device device = _deviceManager.DefaultDevice;

                _currentDevice = device;
                // subscribe to the device events
                SubscribeToDeviceEvents();

                // set acquisition parameters
                device.ShowUI = false;
                device.DisableAfterAcquire = true;

                // open the device
                device.Open();

                // determine if device supports the extended image info
                DeviceCapability extendedImageInfoCap = device.Capabilities.Find(DeviceCapabilityId.IExtImageInfo);
                if (extendedImageInfoCap == null)
                {
                    // close the device
                    device.Close();
                    MessageBox.Show("Device does not support extended image information.");
                    acquireImageButton.Enabled = true;
                    return;
                }

                // specify that image info is necessary
                AddExtendedImageInfoToRetrieveList(device);

                // start the asynchronous image acquisition process 
                device.Acquire();
            }
            catch (TwainException ex)
            {
                MessageBox.Show(GetFullExceptionMessage(ex), "Error");
                acquireImageButton.Enabled = true;
            }
        }

        /// <summary>
        /// Subscribes to the device events.
        /// </summary>
        private void SubscribeToDeviceEvents()
        {
            _currentDevice.ImageAcquired += new EventHandler<ImageAcquiredEventArgs>(device_ImageAcquired);
            _currentDevice.ScanCanceled += new EventHandler(device_ScanCanceled);
            _currentDevice.ScanFailed += new EventHandler<ScanFailedEventArgs>(device_ScanFailed);
            _currentDevice.ScanFinished += new EventHandler(device_ScanFinished);
        }

        /// <summary>
        /// Unsubscribes from the device events.
        /// </summary>
        private void UnsubscribeFromDeviceEvents()
        {
            _currentDevice.ImageAcquired -= new EventHandler<ImageAcquiredEventArgs>(device_ImageAcquired);
            _currentDevice.ScanCanceled -= new EventHandler(device_ScanCanceled);
            _currentDevice.ScanFailed -= new EventHandler<ScanFailedEventArgs>(device_ScanFailed);
            _currentDevice.ScanFinished -= new EventHandler(device_ScanFinished);
        }

        /// <summary>
        /// Image is acquired.
        /// </summary>
        private void device_ImageAcquired(object sender, ImageAcquiredEventArgs e)
        {
            // dispose an acquired image
            e.Image.Dispose();

            // output an extended image info

            extendedImageInfoAboutAcquiredImageTextBox.Text += "IMAGE IS ACQUIRED" + Environment.NewLine;
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            Device device = (Device)sender;
            for (int i = 0; i < device.ExtendedImageInfo.Count; i++)
            {
                AddExtendedImageInfoToResultTextBox(i, device.ExtendedImageInfo[i]);
            }
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;
        }

        /// <summary>
        /// Scan is canceled.
        /// </summary>
        private void device_ScanCanceled(object sender, EventArgs e)
        {
            MessageBox.Show("Scan is canceled.");
        }

        /// <summary>
        /// Scan is failed.
        /// </summary>
        private void device_ScanFailed(object sender, ScanFailedEventArgs e)
        {
            MessageBox.Show(e.ErrorString, "Scan is failed");
        }

        /// <summary>
        /// Scan is finished.
        /// </summary>
        void device_ScanFinished(object sender, EventArgs e)
        {
            // close the device
            _currentDevice.Close();

            acquireImageButton.Enabled = true;
        }


        /// <summary>
        /// Select/unselect all types of extended image info.
        /// </summary>
        private void selectAllExtendedImageInfoButton_Click(object sender, EventArgs e)
        {
            bool selectAll = false;

            if (selectAllExtendedImageInfoButton.Text == "Select all")
            {
                selectAll = true;
                selectAllExtendedImageInfoButton.Text = "Unselect all";
            }
            else
            {
                selectAllExtendedImageInfoButton.Text = "Select all";
            }

            for (int i = 0; i < extendedImageInfoCheckedListBox.Items.Count; i++)
                extendedImageInfoCheckedListBox.SetItemChecked(i, selectAll);
        }

        /// <summary>
        /// Selects standard extended image infos (standard extended image infos always available
        /// if DeviceCapabilityId.IExtImageInfo capability is supported by device).
        /// </summary>
        private void SelectStandardExtendedImageInfos()
        {
            ExtendedImageInfoId[] standardExtendedImageInfoIds = new ExtendedImageInfoId[6] {
                ExtendedImageInfoId.DocumentNumber, ExtendedImageInfoId.PageNumber,
                ExtendedImageInfoId.Camera, ExtendedImageInfoId.FrameNumber,
                ExtendedImageInfoId.Frame, ExtendedImageInfoId.PixelFlavor };

            bool isStandardExtendedImageInfoFound;
            Type enumType = typeof(ExtendedImageInfoId);
            for (int i = 0; i < extendedImageInfoCheckedListBox.Items.Count; i++)
            {
                string extendedImageInfoIdAsString = (string)extendedImageInfoCheckedListBox.Items[i];
                ExtendedImageInfoId extendedImageInfoId = (ExtendedImageInfoId)Enum.Parse(enumType, extendedImageInfoIdAsString);

                isStandardExtendedImageInfoFound = false;
                for (int j = 0; j < standardExtendedImageInfoIds.Length; j++)
                {
                    if (extendedImageInfoId == standardExtendedImageInfoIds[j])
                    {
                        isStandardExtendedImageInfoFound = true;
                        break;
                    }
                }

                extendedImageInfoCheckedListBox.SetItemChecked(i, isStandardExtendedImageInfoFound);
            }
        }

        /// <summary>
        /// Adds type of extended image info to the list of necessary extended image infos.
        /// </summary>
        private void AddExtendedImageInfoToRetrieveList(Device device)
        {
            device.ExtendedImageInfo.Clear();

            Type enumType = typeof(ExtendedImageInfoId);
            for (int i = 0; i < extendedImageInfoCheckedListBox.Items.Count; i++)
            {
                if (extendedImageInfoCheckedListBox.GetItemChecked(i))
                {
                    string extendedImageInfoIdAsString = (string)extendedImageInfoCheckedListBox.Items[i];
                    ExtendedImageInfoId extendedImageInfoId = (ExtendedImageInfoId)Enum.Parse(enumType, extendedImageInfoIdAsString);

                    device.ExtendedImageInfo.Add(new ExtendedImageInfo(extendedImageInfoId));
                }
            }
        }

        /// <summary>
        /// Adds an extended image info to the result.
        /// </summary>
        private void AddExtendedImageInfoToResultTextBox(int index, ExtendedImageInfo info)
        {
            if (!info.IsValueValid)
                return;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("Extended image info {0}", index);
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Name={0}", Enum.GetName(typeof(ExtendedImageInfoId), info.InfoId));
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Id={0}", info.InfoId);
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Value type={0}", info.ValueType);
            extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;

            TwainOneValueContainer oneDeviceCapabilityValue = info.Value as TwainOneValueContainer;
            if (oneDeviceCapabilityValue != null)
            {
                extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("  Value={0}", oneDeviceCapabilityValue.Value);
                extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;
            }
            else
            {
                TwainArrayValueContainer arrayDeviceCapabilityValue = info.Value as TwainArrayValueContainer;
                if (arrayDeviceCapabilityValue != null)
                {
                    extendedImageInfoAboutAcquiredImageTextBox.Text += "Values: ";
                    if (arrayDeviceCapabilityValue.Values != null)
                    {
                        if (arrayDeviceCapabilityValue.Values.GetType() == typeof(byte[]))
                        {
                            extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("byte[{0}]", arrayDeviceCapabilityValue.Values.Length);
                        }
                        else
                        {
                            for (int i = 0; i < arrayDeviceCapabilityValue.Values.Length; i++)
                                extendedImageInfoAboutAcquiredImageTextBox.Text += string.Format("{0}, ", arrayDeviceCapabilityValue.Values.GetValue(i));
                        }
                    }
                    extendedImageInfoAboutAcquiredImageTextBox.Text += Environment.NewLine;
                }
            }
        }


        /// <summary>
        /// Application form is closing.
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_currentDevice != null)
            {
                UnsubscribeFromDeviceEvents();
                _currentDevice = null;
            }

            // close the device manager
            _deviceManager.Close();
            // dispose the device manager
            _deviceManager.Dispose();
        }

        /// <summary>
        /// Returns the message of exception and inner exceptions.
        /// </summary>
        private string GetFullExceptionMessage(Exception ex)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(ex.Message);

            Exception innerException = ex.InnerException;
            while (innerException != null)
            {
                if (ex.Message != innerException.Message)
                    sb.AppendLine(string.Format("Inner exception: {0}", innerException.Message));
                innerException = innerException.InnerException;
            }

            return sb.ToString();
        }

        #endregion

    }
}
