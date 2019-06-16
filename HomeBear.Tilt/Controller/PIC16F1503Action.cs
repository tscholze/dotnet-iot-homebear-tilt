namespace HomeBear.Tilt.Controller
{
    /// <summary>
    /// Available PanTilt HAT actions.
    /// </summary>
    enum PanTiltHATAction
    {
        /// <summary>
        /// Enables servo 1.
        /// </summary>
        EnableServo1,

        /// <summary>
        /// Enables servo 2.
        /// </summary>
        EnableServo2,

        /// <summary>
        /// Disables servo 1.
        /// </summary>
        DisableServo1,

        /// <summary>
        /// Disables servo 2
        /// </summary>
        DisableServo2,

        /// <summary>
        /// Performs a pan move (horizontal).
        /// Additional degree value required.
        /// </summary>
        Pan,

        /// <summary>
        /// Performs a tilt move (vertical).
        /// Additional degree value required.
        /// </summary>
        Tilt
    }
}