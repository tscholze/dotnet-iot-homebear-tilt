using HomeBear.Tilt.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace HomeBear.Tilt.Controller
{
    /// <summary>
    /// Pimoroni original code:
    /// https://github.com/pimoroni/pantilt-hat/blob/master/library/pantilthat/pantilt.py
    /// </summary>
    class PIC16F1503 : IDisposable
    {
        #region Private properties 

        private static string I2C_CONTROLLER_NAME = "I2C1";

        /// <summary>
        /// Address of the PIC16F1503.
        /// </summary>
        private static readonly byte PIC16F1503_I2C_ADDRESS = 0x15;

        /// <summary>
        /// Commando address of servo #1.
        /// </summary>
        private static readonly byte COMMANDO_SERVO_1 = 0x01;

        /// <summary>
        /// Commando address of servo #2.
        /// </summary>
        private static readonly byte COMMANDO_SERVO_2 = 0x03;

        /// <summary>
        /// Commando adress of config setting
        /// </summary>
        private static readonly byte COMMANDO_CONFIG = 0x00;

        /// <summary>
        /// Minimum milliseconds of a servo.
        /// </summary>
        private static readonly int SERVO_MIN_MS = 575;

        /// <summary>
        /// Maximum milliseconds of a servo
        /// </summary>
        private static readonly int SERVO_MAX_MS = 2325;

        /// <summary>
        /// Minimum angle of a servo.
        /// </summary>
        private static readonly int SERVO_MIN_ANGLE = -90;

        /// <summary>
        /// Maximum angle of a servo.
        /// </summary>
        private static readonly int SERVO_MAX_ANGLE = 90;

        /// <summary>
        /// Range in milliseconds between min and max values.
        /// </summary>
        private static readonly int SERVO_RANGE_MS = SERVO_MAX_MS - SERVO_MIN_MS;

        /// <summary>
        /// Determines if servo 1 is currently enabled.
        /// </summary>
        private bool isServo1Enabled = false;

        /// <summary>
        /// Determines if servo 2 is currently enabled.
        /// </summary>
        private bool isServo2Enabled = false;

        /// <summary>
        /// Underlying PIC16F1503 controller.
        /// </summary>
        private I2cDevice pic16f1503;

        /// <summary>
        /// Determins if the `PIC16F1503` has been already
        /// initialized.
        /// </summary>
        private bool IsInitialized = false;

        #endregion

        #region Public Properties

        /// <summary>
        /// Initialieses the PIC16F1503 async.
        /// </summary>
        /// <returns>Init task.</returns>
        public async Task InitAsync()
        {
            // Log start.
            Debug.WriteLine("PIC16F1503 - Init started");

            // Check if controller has already init'ed.
            if (IsInitialized) return;

            // Get controller.
            var i2cDeviceSelector = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);
            IReadOnlyList<DeviceInformation> devices = await DeviceInformation.FindAllAsync(i2cDeviceSelector);

            // Ensure required controller has been found.
            if (devices == null || devices.Count == 0)
            {
                throw new DeviceNotFoundException("No devices found.");
            }

            // Setup devices.
            var settings = new I2cConnectionSettings(PIC16F1503_I2C_ADDRESS) { BusSpeed = I2cBusSpeed.FastMode };
            pic16f1503 = await I2cDevice.FromIdAsync(devices[0].Id, settings);

            // Ensure required device has been found.
            if (pic16f1503 == null)
            {
                throw new DeviceNotFoundException("PIC16F1503 not found."); 
            }

            // Set flag that controller has been init'ed.
            IsInitialized = true;

            // Write inital setup to controller.
            WriteConfiguration();

            // Log end.
            Debug.WriteLine("PIC16F1503 - Init finished");
        }

        internal void PanDegrees()
        {
            GetDegrees();
        }

        /// <summary>
        /// Validates if given value is in (inclusive) given min and max values.
        /// </summary>
        /// <param name="value">Value to inspect.</param>
        /// <param name="min">Minimum value.</param>
        /// <param name="max">Maximum value.</param>
        /// <returns>True if valid.</returns>
        private bool ValidateRange(int value, int min, int max)
        {
            return (value >= min && value <= max);
        }

        /// <summary>
        /// Converts milliseconds to degrees.
        /// </summary>
        /// <param name="pulse">Pulse in milliseconds value to convert.</param>
        /// <returns>Converted degree value.</returns>
        private int MsToAngle(int pulse)
        {
            // Ensure range is valid.
            if (!ValidateRange(pulse, SERVO_MIN_MS, SERVO_MAX_MS))
            {
                throw new InvalidOperationException($"Given ms of {pulse} is less than {SERVO_MIN_MS} or greater than {SERVO_MAX_MS}");
            }

            // Perform calculation.
            var angle = (pulse - SERVO_MIN_MS) / (float)SERVO_RANGE_MS * 180;
            return ((int)Math.Round(angle, 0)) - 90;
        }


        /// <summary>
        /// Converts degrees to milliseconds.
        /// </summary>
        /// <param name="degrees">Degree value to convert.</param>
        /// <returns>Converted millisecond value.</returns>
        private int DegreesToMs(int degrees)
        {
            // Ensure range is valid.
            if (!ValidateRange(degrees, SERVO_MIN_ANGLE, SERVO_MAX_ANGLE))
            {
                throw new InvalidOperationException($"Given angle of {degrees} is less than {SERVO_MIN_ANGLE} or greater than {SERVO_MAX_ANGLE}");
            }

            degrees += SERVO_MAX_ANGLE;

            // Perform calculation.
            return SERVO_MIN_MS + (SERVO_RANGE_MS / 180) * degrees;
        }

        private void GetDegrees()
        {
            byte[] readBuffer = new byte[2];
            var readResult = pic16f1503.WriteReadPartial(new byte[] { COMMANDO_SERVO_1 }, readBuffer);

            var angle = MsToAngle(readBuffer[0] | (readBuffer[1] << 8));
            Debug.WriteLine($"AAAANGLE: {angle}");
        }

            /// <summary>
            /// Sets degree value to specified roation action.
            /// </summary>
            /// <param name="action">Executed action.</param>
            /// <param name="degrees">New degree value.</param>
            private void SetDegrees(PIC16F1503Action action, int degrees)
        {
            // TODO: Abstract it to use servo property
            if(!isServo1Enabled)
            {
                PerformAction(PIC16F1503Action.EnableServo1);
            }

            // Build data bytes
            // Order: [CMD, DATA]
            var ms = DegreesToMs(degrees);
            WriteByte(COMMANDO_SERVO_1, BitConverter.GetBytes(ms));
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the pic16f1503 controller.
        /// </summary>
        public void Dispose()
        {
            // Dispose device.
            if (pic16f1503 != null)
            {
                PerformAction(PIC16F1503Action.DisableServo1);
                PerformAction(PIC16F1503Action.DisableServo2);
                pic16f1503.Dispose();
                pic16f1503 = null;
            }
        }

        #endregion

        #region Private helper 

        /// <summary>
        /// Triggeres calls to perform given action.
        /// </summary>
        /// <param name="action">Action that will be peformed.</param>
        private void PerformAction(PIC16F1503Action action)
        {
            switch (action)
            {
                case PIC16F1503Action.EnableServo1:
                    isServo1Enabled = true;
                    WriteConfiguration();
                    break;

                case PIC16F1503Action.EnableServo2:
                    isServo2Enabled = true;
                    WriteConfiguration();
                    break;

                case PIC16F1503Action.DisableServo1:
                    isServo1Enabled = false;
                    WriteConfiguration();
                    break;

                case PIC16F1503Action.DisableServo2:
                    isServo1Enabled = false;
                    WriteConfiguration();
                    break;

                case PIC16F1503Action.Pan:
                    SetDegrees(PIC16F1503Action.Pan, 50);
                    break;
            }
        }

        /// <summary>
        /// Write data for given command.
        /// </summary>
        /// <param name="command">Command value.</param>
        /// <param name="data">Data value.</param>
        private bool WriteByte(byte command, byte[] data)
        {
            // Update data with command register.
            var list = data.ToList();
            list.Insert(0, command);

            // Log
            var text = "";
            list.ForEach(b => text += ($" [{b.ToString()}]"));
            Debug.WriteLine($"Writing follwing byte sequence to the device: {text}");

            // Write to device.
            var writeResult = pic16f1503.WritePartial(list.ToArray());

            // Check if transfer was successful.
            return writeResult.Status == I2cTransferStatus.FullTransfer;
        }

        /// <summary>
        /// Writes current configuration to PIC16F1503.
        /// </summary>
        private void WriteConfiguration()
        {
            // Create configuration mask.
            byte config = 0;

            // Enable servo 1
            config |= 1;

            // Enable servo 2
            config |= 0 << 1;

            // Enable lights
            config |= 0 << 2;

            // Light mode
            config |= 0 << 3;

            // Light on
            config |= 0 << 4;

            // Write configuration to device.
            WriteByte(COMMANDO_CONFIG, new byte[] { config });
        }

        #endregion

        #region Public helper 

        /// <summary>
        /// Pans the arm to the given degree value.
        /// </summary>
        /// <param name="degrees">New degree value</param>
        public void Pan(int degrees)
        {
            SetDegrees(PIC16F1503Action.Pan, degrees);
        }

        #endregion
    }
}