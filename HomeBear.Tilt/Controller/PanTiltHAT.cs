using HomeBear.Tilt.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.System.Threading;

namespace HomeBear.Tilt.Controller
{
    /// <summary>
    /// This class helps to send controls and read values from the PanTilt HAT (PIC16F1503).
    /// This is a C# port of the offical Pimoroni Python library.
    /// 
    /// Links:
    ///     - Pimoroni product page:
    ///         https://shop.pimoroni.com/products/pan-tilt-hat
    ///         
    ///     - Pimoroni source:
    ///         https://github.com/pimoroni/pantilt-hat/blob/master/library/pantilthat/pantilt.py
    ///         
    ///     - GPIO pin scheme: 
    ///         https://pinout.xyz/pinout/pan_tilt_hat
    /// </summary>
    class PanTiltHAT : IDisposable
    {
        #region Private properties 

        /// <summary>
        /// I2C controller name.
        /// </summary>
        private static readonly string I2C_CONTROLLER_NAME = "I2C1";

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
        /// Time out after a servo will be disabled.
        /// </summary>
        private static readonly int SERVO_IDLE_TIMEOUT_SECONDS = 2;

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
        /// Determins if the `PIC16F1503` has been already
        /// initialized.
        /// </summary>
        private bool IsInitialized = false;

        /// <summary>
        /// Underlying PIC16F1503 controller.
        /// </summary>
        private I2cDevice pic16f1503;

        /// <summary>
        /// Servo time out timer.
        /// </summary>
        private ThreadPoolTimer servoTimeOutTimer;

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the pic16f1503 controller.
        /// </summary>
        public void Dispose()
        {
            // Dispose timers.
            if (servoTimeOutTimer != null)
            {
                servoTimeOutTimer.Cancel();
                servoTimeOutTimer = null;
            }

            // Dispose device.
            if (pic16f1503 != null)
            {
                PerformAction(PanTiltHATAction.DisableServo1);
                PerformAction(PanTiltHATAction.DisableServo2);
                pic16f1503.Dispose();
                pic16f1503 = null;
                IsInitialized = false;
            }
        }

        #endregion

        #region Private helper 

        /// <summary>
        /// Triggeres calls to perform given action.
        /// </summary>
        /// <param name="action">Action that will be peformed.</param>
        private void PerformAction(PanTiltHATAction action)
        {
            switch (action)
            {
                case PanTiltHATAction.EnableServo1:
                    isServo1Enabled = true;
                    WriteConfiguration();
                    break;

                case PanTiltHATAction.EnableServo2:
                    isServo2Enabled = true;
                    WriteConfiguration();
                    break;

                case PanTiltHATAction.DisableServo1:
                    isServo1Enabled = false;
                    WriteConfiguration();
                    break;

                case PanTiltHATAction.DisableServo2:
                    isServo1Enabled = false;
                    WriteConfiguration();
                    break;

                case PanTiltHATAction.Pan:
                    SetDegrees(PanTiltHATAction.Pan, 50);
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
            LogBytes(list);

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
            // LED values are currently not supported.
            // Values to set:
            //  0 = off
            //  1 = on

            // Create configuration mask.
            byte config = 0;

            // Enable servo 1
            config |= Convert.ToByte(isServo1Enabled);

            // Enable servo 2
            config |= Convert.ToByte(Convert.ToInt32(isServo2Enabled) << 1);

            // Enable lights
            config |= 0 << 2;

            // Light mode
            config |= 0 << 3;

            // Light on
            config |= 0 << 4;

            // Write configuration to device.
            WriteByte(COMMANDO_CONFIG, new byte[] { config });
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
        private int MsToDegrees(int pulse)
        {
            // Ensure range is valid.
            if (!ValidateRange(pulse, SERVO_MIN_MS, SERVO_MAX_MS))
            {
                throw new InvalidOperationException($"Given ms of {pulse} is less than {SERVO_MIN_MS} or greater than {SERVO_MAX_MS}");
            }

            // Perform calculation.
            var angle = (pulse - SERVO_MIN_MS) / (float)SERVO_RANGE_MS * 180;
            return ((int)Math.Round(angle, 0)) - SERVO_MAX_ANGLE;
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

        /// <summary>
        /// Gets the degrees of given pan or tilt action.
        /// </summary>
        /// <param name="action">Defines wether pan or tilt degrees will be returned.</param>
        /// <returns>Current degrees of pan or tilt action.</returns>
        private int GetDegrees(PanTiltHATAction action)
        {
            // Ensure valid action value.
            if (action != PanTiltHATAction.Tilt && action != PanTiltHATAction.Pan)
            {
                throw new OperationCanceledException("Action must be .Tilt or .Pan");
            }

            // Check if tilt or pan.
            var isTiltRequested = action == PanTiltHATAction.Tilt;

            // Read from device
            var commando = isTiltRequested ? COMMANDO_SERVO_1 : COMMANDO_SERVO_2;
            byte[] readBuffer = new byte[2];
            _ = pic16f1503.WriteReadPartial(new byte[] { commando }, readBuffer);

            // Convert pulses to degrees.
            var degrees = MsToDegrees(readBuffer[0] | (readBuffer[1] << 8));
            Debug.WriteLine($"Found `{degrees}` degrees for action `{action.ToString()}` ");

            // Return value.
            return degrees;
        }

        /// <summary>
        /// Sets degree value to specified rotating action.
        /// Will reset the servo time out timer.
        /// </summary>
        /// <param name="action">Executed action.</param>
        /// <param name="degrees">New degree value.</param>
        private void SetDegrees(PanTiltHATAction action, int degrees)
        {
            // Ensure valid action value.
            if (action != PanTiltHATAction.Tilt && action != PanTiltHATAction.Pan)
            {
                throw new OperationCanceledException("Action must be .Tilt or .Pan");
            }

            // Stop existing time out timer.
            StopServoTimeOutTimer();

            // Check if tilt or pan.
            var isTiltRequested = action == PanTiltHATAction.Tilt;

            // Enable servo is required.
            if (!(isTiltRequested ? isServo1Enabled : isServo2Enabled))
            {
                PerformAction(isTiltRequested ? PanTiltHATAction.EnableServo1 : PanTiltHATAction.EnableServo2);
            }

            // Build data bytes.
            var command = isTiltRequested ? COMMANDO_SERVO_1 : COMMANDO_SERVO_2;
            var bitmask = BitConverter.GetBytes(DegreesToMs(degrees));

            // Write to device.
            WriteByte(command, bitmask);

            // Start servo time out timer.
            StartServoTimeOutTimer();
        }

        /// <summary>
        /// Will start the servo time out timer.
        /// </summary>
        private void StartServoTimeOutTimer()
        {
            servoTimeOutTimer = ThreadPoolTimer.CreateTimer(TimeOutTimer_Tick, TimeSpan.FromSeconds(SERVO_IDLE_TIMEOUT_SECONDS));
        }

        /// <summary>
        /// Will stop / cancel the servo time out timer.
        /// </summary>
        private void StopServoTimeOutTimer()
        {
            if (servoTimeOutTimer != null)
            {
                servoTimeOutTimer.Cancel();
            }
        }

        /// <summary>
        /// Will disable all servos.
        /// </summary>
        /// <param name="timer">Underyling timer</param>
        private void TimeOutTimer_Tick(ThreadPoolTimer timer)
        {
            // Disable both servos.
            isServo1Enabled = false;
            isServo2Enabled = false;

            // Write config to device.
            WriteConfiguration();
        }

        /// <summary>
        /// Logs given byte list.
        /// </summary>
        /// <param name="list">List of bytes.</param>
        private void LogBytes(List<byte> list)
        {
            var text = "";
            list.ForEach(b => text += ($" [{b.ToString()}]"));
            Debug.WriteLine($"Writing follwing byte sequence to the device: {text.Trim()}");
        }

        #endregion

        #region Public helper 

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

        /// <summary>
        /// Pans (horizontal) the arm to the given degree value.
        /// </summary>
        /// <param name="degrees">New degree value</param>
        public void Pan(int degrees)
        {
            SetDegrees(PanTiltHATAction.Pan, degrees);
        }

        /// <summary>
        /// Tilts (vertical) the arm to the given degree value.
        /// </summary>
        /// <param name="degrees">New degree value</param>
        public void Tilt(int degrees)
        {
            SetDegrees(PanTiltHATAction.Tilt, degrees);
        }

        /// <summary>
        /// Gets the current pan (horizontal) position in degrees.
        /// </summary>
        /// <returns>Degrees value</returns>
        public int PanDegrees()
        {
            return GetDegrees(PanTiltHATAction.Pan);
        }

        /// <summary>
        /// Gets the current tilt (vertical) position in degrees.
        /// </summary>
        /// <returns>Degrees value</returns>
        public int TiltDegrees()
        {
            return GetDegrees(PanTiltHATAction.Tilt);
        }

        #endregion
    }
}