using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace HomeBear.Tilt.Utils
{
    /// <summary>
    /// Event args for the case that faces has been detected.
    /// </summary>
    class FaceRectsDetectedEventArgs: EventArgs
    {
        #region Public properties

        /// <summary>
        /// Detected faces.
        /// </summary>
        public IEnumerable<Rect> faceRects;

        #endregion

        #region Constructors

        /// <summary>
        /// Init.
        /// </summary>
        /// <param name="faceRects">Detected faces.</param>
        public FaceRectsDetectedEventArgs(IEnumerable<Rect> faceRects)
        {
            this.faceRects = faceRects;
        }

        #endregion
    }
}
