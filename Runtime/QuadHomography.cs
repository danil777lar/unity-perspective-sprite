using UnityEngine;

namespace Larje.PerspectiveSprite
{
    public readonly struct QuadHomography
    {
        private readonly bool _isProjective;
        private readonly float _a;
        private readonly float _b;
        private readonly float _c;
        private readonly float _d;
        private readonly float _e;
        private readonly float _f;
        private readonly float _g;
        private readonly float _h;
        private readonly Vector2 _bottomLeft;
        private readonly Vector2 _bottomRight;
        private readonly Vector2 _topRight;
        private readonly Vector2 _topLeft;

        private QuadHomography( Vector2 bottomLeft, Vector2 bottomRight, Vector2 topRight, Vector2 topLeft, bool isProjective, float a, float b, float c, float d, float e, float f, float g, float h )
        {
            _bottomLeft = bottomLeft;
            _bottomRight = bottomRight;
            _topRight = topRight;
            _topLeft = topLeft;
            _isProjective = isProjective;
            _a = a;
            _b = b;
            _c = c;
            _d = d;
            _e = e;
            _f = f;
            _g = g;
            _h = h;
        }

        public static QuadHomography FromCorners( Vector2 bottomLeft, Vector2 bottomRight, Vector2 topRight, Vector2 topLeft, float epsilon )
        {
            float sx = bottomLeft.x - bottomRight.x + topRight.x - topLeft.x;
            float sy = bottomLeft.y - bottomRight.y + topRight.y - topLeft.y;
            float dx1 = bottomRight.x - topRight.x;
            float dx2 = topLeft.x - topRight.x;
            float dy1 = bottomRight.y - topRight.y;
            float dy2 = topLeft.y - topRight.y;
            float determinant = dx1 * dy2 - dx2 * dy1;

            bool isParallelogram = Mathf.Abs( sx ) < epsilon && Mathf.Abs( sy ) < epsilon;
            if ( isParallelogram || Mathf.Abs( determinant ) < epsilon )
            {
                return new QuadHomography( bottomLeft, bottomRight, topRight, topLeft, false, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f );
            }

            float g = ( sx * dy2 - dx2 * sy ) / determinant;
            float h = ( dx1 * sy - sx * dy1 ) / determinant;
            float a = bottomRight.x - bottomLeft.x + g * bottomRight.x;
            float b = topLeft.x - bottomLeft.x + h * topLeft.x;
            float d = bottomRight.y - bottomLeft.y + g * bottomRight.y;
            float e = topLeft.y - bottomLeft.y + h * topLeft.y;

            return new QuadHomography( bottomLeft, bottomRight, topRight, topLeft, true, a, b, bottomLeft.x, d, e, bottomLeft.y, g, h );
        }

        public Vector2 Map( Vector2 normalized )
        {
            if ( !_isProjective )
            {
                return MapBilinear( normalized );
            }

            float w = _g * normalized.x + _h * normalized.y + 1f;
            if ( Mathf.Approximately( w, 0f ) )
            {
                return MapBilinear( normalized );
            }

            float x = ( _a * normalized.x + _b * normalized.y + _c ) / w;
            float y = ( _d * normalized.x + _e * normalized.y + _f ) / w;
            return new Vector2( x, y );
        }

        private Vector2 MapBilinear( Vector2 normalized )
        {
            Vector2 bottom = Vector2.LerpUnclamped( _bottomLeft, _bottomRight, normalized.x );
            Vector2 top = Vector2.LerpUnclamped( _topLeft, _topRight, normalized.x );
            return Vector2.LerpUnclamped( bottom, top, normalized.y );
        }
    }
}
