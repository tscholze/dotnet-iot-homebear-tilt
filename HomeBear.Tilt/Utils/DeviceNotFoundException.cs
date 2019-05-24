using System;

namespace HomeBear.Tilt.Utils
{
    public class DeviceNotFoundException : Exception
    {
        public DeviceNotFoundException()
        {
        }

        public DeviceNotFoundException(string message)
        : base(message)
        {
        }
    }
}
