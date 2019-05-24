using HomeBear.Tilt.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        /// <summary>
        /// Underlying shared instance.
        /// </summary>
        private static readonly PIC16F1503 instance = new PIC16F1503();

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
        /// Default instance of BlinktController.
        /// </summary>
        public static PIC16F1503 Default
        {
            get
            {
                return instance;
            }
        }

        #endregion

        #region Constructor, Init & Deconstructor

        static PIC16F1503()
        {
        }

        private PIC16F1503()
        {
        }

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
            string i2cDeviceSelector = I2cDevice.GetDeviceSelector();
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


            // DEV
            PerformAction(PIC16F1503Action.Pan);
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
        /// <param name="ms">Milliseconds value to convert.</param>
        /// <returns>Converted degree value.</returns>
        private int MsToAngle(int ms)
        {
            if(!ValidateRange(ms, SERVO_MIN_MS, SERVO_MAX_MS))
            {
                throw new InvalidOperationException($"Given ms of {ms} is less than {SERVO_MIN_MS} or greater than {SERVO_MAX_MS}");
            }

            // TODO: Why casting.-
            var angle = (Convert.ToDouble(ms - SERVO_MIN_MS) / Convert.ToDouble(SERVO_RANGE_MS)) * 180.0;

            return Convert.ToInt32(angle) - SERVO_MAX_ANGLE;
        }

        /// <summary>
        /// Converts degrees to milliseconds.
        /// </summary>
        /// <param name="degrees">Degree value to convert.</param>
        /// <returns>Converted millisecond value.</returns>
        private int DegreesToMs(int degrees)
        {
            // Validate range
            if (!ValidateRange(degrees, SERVO_MIN_ANGLE, SERVO_MAX_ANGLE))
            {
                throw new InvalidOperationException($"Given angle of {degrees} is less than {SERVO_MIN_ANGLE} or greater than {SERVO_MAX_ANGLE}");
            }

            var ms = SERVO_MIN_MS + ((SERVO_RANGE_MS / 180) * (degrees + SERVO_MAX_ANGLE));

            System.Console.WriteLine($"Convereted {degrees} to {ms} ms");
            return ms;
        }

        /// <summary>
        /// Gets degrees from servo.
        /// </summary>
        /// <returns>Degree of servo</returns>
        private int GetDegrees()
        {
            // Read data from device.
            var readBuffer = new byte[4];
            var result = pic16f1503.ReadPartial(readBuffer);

            // TODO: Implement it
            System.Console.WriteLine($"Result: {result}");
            foreach (var b in readBuffer)
            {
                System.Console.WriteLine($"{b}");
            }

            return 100;
        }

        /// <summary>
        /// Sets degree value to specified roation action.
        /// </summary>
        /// <param name="action">Executed action.</param>
        /// <param name="degrees">New degree value.</param>
        private void SetDegrees(PIC16F1503Action action, int degrees)
        {
            // TODO: Abstract it to use servo propertx
            if(!isServo1Enabled)
            {
                PerformAction(PIC16F1503Action.EnableServo1);
            }

            // Build data bytes
            // Order: [CMD, DATA]
            var ms = DegreesToMs(degrees);
            var data = BitConverter.GetBytes(ms).ToList();
            data.Insert(0, COMMANDO_SERVO_1);

            // Log Start.
            Debug.WriteLine($"PIC16F1503 - SetDegrees to {ms} start.\nData: {data}");

            // Write to device
            pic16f1503.Write(data.ToArray());

            // Log end.
            Debug.WriteLine("PIC16F1503 - SetDegrees finished");
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
                    break;

                case PIC16F1503Action.EnableServo2:
                    isServo2Enabled = true;
                    break;

                case PIC16F1503Action.DisableServo1:
                    isServo1Enabled = false;
                    break;

                case PIC16F1503Action.DisableServo2:
                    isServo1Enabled = false;
                    break;

                case PIC16F1503Action.Pan:
                    SetDegrees(PIC16F1503Action.Pan, 65);
                    break;
            }

            WriteConfiguration();
        }

        /// <summary>
        /// Write data for given command.
        /// </summary>
        /// <param name="command">Command value.</param>
        /// <param name="data">Data value.</param>
        private void WriteByte(byte command, byte[] data)
        {
            pic16f1503.Write(data);
        }

        /// <summary>
        /// Writes current configuration to PIC16F1503.
        /// </summary>
        private void WriteConfiguration()
        {
            // Create configuration mask.
            byte config = 0;
            config |= 1;
            config |= 0 << 1;
            config |= 0 << 2;
            config |= 0 << 3;
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
           // SetDegrees(servo1, degrees);
        }

        /// <summary>
        /// Tilts the arm to the given degree value.
        /// </summary>
        /// <param name="degrees">New degree value</param>
        public void Tilt(int degrees)
        {
            // SetDegrees(servo1, degrees);
        }

        #endregion
    }
}

enum PIC16F1503Action
{
    EnableServo1,
    EnableServo2,
    DisableServo1,
    DisableServo2,
    Pan,
    Tilt
}
