using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Larje.PerspectiveSprite
{
    [ ExecuteAlways ]
    [ DisallowMultipleComponent ]
    [ RequireComponent( typeof( MeshFilter ), typeof( MeshRenderer ) ) ]
    public sealed class PerspectiveSpriteRenderer : MonoBehaviour
    {
        public const int CornerCount = 4;
        public const int MinSubdivisions = 1;
        public const int MaxSubdivisions = 32;

        private const float DegenerateEpsilon = 1e-6f;
        private const string MeshName = "PerspectiveSpriteMesh";

        private static readonly int MainTexId = Shader.PropertyToID( "_MainTex" );

        [ Header( "Sprite" ) ]
        [ SerializeField ] private Sprite _sprite;
        [ SerializeField ] private Color _color = Color.white;
        [ SerializeField ] private bool _flipX;
        [ SerializeField ] private bool _flipY;

        [ Header( "Quad" ) ]
        [ SerializeField, Range( MinSubdivisions, MaxSubdivisions ) ] private int _subdivisions = 8;
        [ SerializeField ] private Vector2 _bottomLeft = new Vector2( -0.5f, -0.5f );
        [ SerializeField ] private Vector2 _topLeft = new Vector2( -0.5f, 0.5f );
        [ SerializeField ] private Vector2 _topRight = new Vector2( 0.5f, 0.5f );
        [ SerializeField ] private Vector2 _bottomRight = new Vector2( 0.5f, -0.5f );

        private bool _isDirty = true;
        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private SecondarySpriteTexture[] _secondaryTextures;
        private readonly List< Vector3 > _vertices = new List< Vector3 >();
        private readonly List< Vector2 > _uvs = new List< Vector2 >();
        private readonly List< Color > _colors = new List< Color >();
        private readonly List< Vector3 > _normals = new List< Vector3 >();
        private readonly List< Vector4 > _tangents = new List< Vector4 >();
        private readonly List< int > _triangles = new List< int >();

        public Sprite Sprite
        {
            get
            {
                return _sprite;
            }
            set
            {
                if ( _sprite == value )
                {
                    return;
                }

                _sprite = value;
                SetDirty();
            }
        }

        public Color Color
        {
            get
            {
                return _color;
            }
            set
            {
                if ( _color == value )
                {
                    return;
                }

                _color = value;
                SetDirty();
            }
        }

        public bool FlipX
        {
            get
            {
                return _flipX;
            }
            set
            {
                _flipX = value;
                SetDirty();
            }
        }

        public bool FlipY
        {
            get
            {
                return _flipY;
            }
            set
            {
                _flipY = value;
                SetDirty();
            }
        }

        public int Subdivisions
        {
            get
            {
                return _subdivisions;
            }
            set
            {
                _subdivisions = Mathf.Clamp( value, MinSubdivisions, MaxSubdivisions );
                SetDirty();
            }
        }

        public MeshRenderer MeshRenderer
        {
            get
            {
                CacheComponents();
                return _meshRenderer;
            }
        }

        public Vector2 GetCorner( QuadCorner corner )
        {
            switch ( corner )
            {
                case QuadCorner.BottomLeft:
                    return _bottomLeft;
                case QuadCorner.TopLeft:
                    return _topLeft;
                case QuadCorner.TopRight:
                    return _topRight;
                default:
                    return _bottomRight;
            }
        }

        public void SetCorner( QuadCorner corner, Vector2 position )
        {
            switch ( corner )
            {
                case QuadCorner.BottomLeft:
                    _bottomLeft = position;
                    break;
                case QuadCorner.TopLeft:
                    _topLeft = position;
                    break;
                case QuadCorner.TopRight:
                    _topRight = position;
                    break;
                default:
                    _bottomRight = position;
                    break;
            }

            SetDirty();
        }

        public void SetCorners( Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight )
        {
            _bottomLeft = bottomLeft;
            _topLeft = topLeft;
            _topRight = topRight;
            _bottomRight = bottomRight;
            SetDirty();
        }

        public void ResetCornersToSprite()
        {
            Rect rect = GetSpriteLocalRect();
            SetCorners( rect.min, new Vector2( rect.xMin, rect.yMax ), rect.max, new Vector2( rect.xMax, rect.yMin ) );
        }

        public Vector2 Evaluate( Vector2 normalized )
        {
            QuadHomography homography = QuadHomography.FromCorners( _bottomLeft, _bottomRight, _topRight, _topLeft, DegenerateEpsilon );
            return homography.Map( normalized );
        }

        public void SetDirty()
        {
            _isDirty = true;
        }

        public void Rebuild()
        {
            CacheComponents();
            EnsureMesh();
            BuildMesh();
            ApplyProperties();
            _isDirty = false;
        }

        private void Reset()
        {
            CacheComponents();

            if ( _sprite == null )
            {
                SpriteRenderer spriteRenderer = GetComponent< SpriteRenderer >();
                if ( spriteRenderer != null )
                {
                    _sprite = spriteRenderer.sprite;
                }
            }

            if ( _meshRenderer.sharedMaterial == null )
            {
                _meshRenderer.sharedMaterial = FindDefaultMaterial();
            }

            ResetCornersToSprite();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            if ( _meshFilter != null && _meshFilter.sharedMesh == _mesh )
            {
                _meshFilter.sharedMesh = null;
            }
        }

        private void OnDestroy()
        {
            DestroyMesh();
        }

        private void OnValidate()
        {
            _subdivisions = Mathf.Clamp( _subdivisions, MinSubdivisions, MaxSubdivisions );
            SetDirty();
        }

        private void LateUpdate()
        {
            if ( _isDirty )
            {
                Rebuild();
            }
        }

        private void CacheComponents()
        {
            if ( _meshFilter == null )
            {
                _meshFilter = GetComponent< MeshFilter >();
            }

            if ( _meshRenderer == null )
            {
                _meshRenderer = GetComponent< MeshRenderer >();
            }
        }

        private void EnsureMesh()
        {
            if ( _mesh == null )
            {
                _mesh = new Mesh();
                _mesh.name = MeshName;
                _mesh.hideFlags = HideFlags.HideAndDontSave;
                _mesh.MarkDynamic();
            }

            if ( _meshFilter.sharedMesh != _mesh )
            {
                _meshFilter.sharedMesh = _mesh;
            }
        }

        private void DestroyMesh()
        {
            if ( _mesh == null )
            {
                return;
            }

            if ( Application.isPlaying )
            {
                Destroy( _mesh );
            }
            else
            {
                DestroyImmediate( _mesh );
            }

            _mesh = null;
        }

        private void BuildMesh()
        {
            _vertices.Clear();
            _uvs.Clear();
            _colors.Clear();
            _normals.Clear();
            _tangents.Clear();
            _triangles.Clear();
            _mesh.Clear();

            if ( _sprite == null )
            {
                return;
            }

            QuadHomography homography = QuadHomography.FromCorners( _bottomLeft, _bottomRight, _topRight, _topLeft, DegenerateEpsilon );
            Rect uvRect = GetSpriteUvRect();
            int lineCount = _subdivisions + 1;

            for ( int y = 0; y < lineCount; y++ )
            {
                float v = ( float ) y / _subdivisions;

                for ( int x = 0; x < lineCount; x++ )
                {
                    float u = ( float ) x / _subdivisions;
                    float uvU = _flipX ? 1f - u : u;
                    float uvV = _flipY ? 1f - v : v;

                    _vertices.Add( homography.Map( new Vector2( u, v ) ) );
                    _uvs.Add( new Vector2( Mathf.Lerp( uvRect.xMin, uvRect.xMax, uvU ), Mathf.Lerp( uvRect.yMin, uvRect.yMax, uvV ) ) );
                    _colors.Add( _color );
                    _normals.Add( Vector3.back );
                    _tangents.Add( new Vector4( 1f, 0f, 0f, -1f ) );
                }
            }

            for ( int y = 0; y < _subdivisions; y++ )
            {
                for ( int x = 0; x < _subdivisions; x++ )
                {
                    int bottomLeft = y * lineCount + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + lineCount;
                    int topRight = topLeft + 1;

                    _triangles.Add( bottomLeft );
                    _triangles.Add( topLeft );
                    _triangles.Add( topRight );
                    _triangles.Add( bottomLeft );
                    _triangles.Add( topRight );
                    _triangles.Add( bottomRight );
                }
            }

            _mesh.SetVertices( _vertices );
            _mesh.SetUVs( 0, _uvs );
            _mesh.SetColors( _colors );
            _mesh.SetNormals( _normals );
            _mesh.SetTangents( _tangents );
            _mesh.SetTriangles( _triangles, 0 );
            _mesh.RecalculateBounds();
        }

        private void ApplyProperties()
        {
            if ( _propertyBlock == null )
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _meshRenderer.GetPropertyBlock( _propertyBlock );

            if ( _sprite != null )
            {
                _propertyBlock.SetTexture( MainTexId, _sprite.texture );
                ApplySecondaryTextures();
            }

            _meshRenderer.SetPropertyBlock( _propertyBlock );
        }

        private void ApplySecondaryTextures()
        {
            int count = _sprite.GetSecondaryTextureCount();
            if ( count == 0 )
            {
                return;
            }

            if ( _secondaryTextures == null || _secondaryTextures.Length < count )
            {
                _secondaryTextures = new SecondarySpriteTexture[ count ];
            }

            _sprite.GetSecondaryTextures( _secondaryTextures );

            for ( int i = 0; i < count; i++ )
            {
                SecondarySpriteTexture secondary = _secondaryTextures[ i ];
                if ( secondary.texture != null && !string.IsNullOrEmpty( secondary.name ) )
                {
                    _propertyBlock.SetTexture( secondary.name, secondary.texture );
                }
            }
        }

        private Rect GetSpriteUvRect()
        {
            Texture2D texture = _sprite.texture;
            Rect rect = _sprite.textureRect;
            return new Rect( rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height );
        }

        private Rect GetSpriteLocalRect()
        {
            if ( _sprite == null )
            {
                return new Rect( -0.5f, -0.5f, 1f, 1f );
            }

            Rect textureRect = _sprite.textureRect;
            Vector2 size = textureRect.size / _sprite.pixelsPerUnit;
            Vector2 offset = ( _sprite.textureRectOffset - _sprite.pivot ) / _sprite.pixelsPerUnit;
            return new Rect( offset, size );
        }

        private Material FindDefaultMaterial()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if ( pipeline == null )
            {
                return null;
            }

            return pipeline.default2DMaterial;
        }
    }
}
