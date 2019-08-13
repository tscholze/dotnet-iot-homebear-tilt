using System;

namespace HomeBear.Tilt.Utils
{
    /// <summary>
    /// Event args with a given message.
    /// </summary>
    class MessageEventArgs : EventArgs
    {
        #region Public properties

        /// <summary>
        /// Message of the event args.
        /// </summary>
        public string Message { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Init.
        /// </summary>
        /// <param name="message">Message of the event args.</param>
        public MessageEventArgs(string message)
        {
            Message = message;
        }

        #endregion
    }
}
