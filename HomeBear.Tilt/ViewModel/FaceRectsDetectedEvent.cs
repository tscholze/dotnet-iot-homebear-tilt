using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace HomeBear.Tilt.ViewModel
{
    class FaceRectsDetectedEvent: EventArgs
    {
        #region Public properties

        public IEnumerable<Rect> faceRects;

        #endregion

        #region Constructors

        public FaceRectsDetectedEvent(IEnumerable<Rect> faceRects)
        {
            this.faceRects = faceRects;
        }

        #endregion
    }
}
