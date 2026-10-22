using UnityEditor;
using UnityEngine;

namespace Larje.PerspectiveSprite.Editor
{
    [ CustomEditor( typeof( PerspectiveSpriteRenderer ) ) ]
    [ CanEditMultipleObjects ]
    public sealed class PerspectiveSpriteRendererEditor : UnityEditor.Editor
    {
        private const int GridLineCount = 4;
        private const float HandleSize = 0.06f;
        private const float OutlineWidth = 3f;
        private const string UndoName = "Move Perspective Sprite Corner";

        private static readonly Color OutlineColor = new Color( 0.31f, 0.78f, 0.84f, 1f );
        private static readonly Color GridColor = new Color( 0.31f, 0.78f, 0.84f, 0.35f );
        private static readonly Color HandleColor = new Color( 1f, 0.64f, 0.27f, 1f );
        private static readonly QuadCorner[] Corners = { QuadCorner.BottomLeft, QuadCorner.TopLeft, QuadCorner.TopRight, QuadCorner.BottomRight };

        private bool _editCorners = true;
        private string[] _sortingLayerNames;
        private int[] _sortingLayerIds;
        private readonly Vector3[] _outline = new Vector3[ 5 ];

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding( serializedObject, "m_Script" );
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawSorting();
            EditorGUILayout.Space();
            DrawCornerTools();
        }

        private void OnSceneGUI()
        {
            if ( !_editCorners )
            {
                return;
            }

            PerspectiveSpriteRenderer spriteRenderer = ( PerspectiveSpriteRenderer ) target;
            Transform transform = spriteRenderer.transform;

            DrawQuadPreview( spriteRenderer, transform );

            for ( int i = 0; i < Corners.Length; i++ )
            {
                DrawCornerHandle( spriteRenderer, transform, Corners[ i ] );
            }
        }

        private void DrawCornerHandle( PerspectiveSpriteRenderer spriteRenderer, Transform transform, QuadCorner corner )
        {
            Vector3 worldPosition = transform.TransformPoint( spriteRenderer.GetCorner( corner ) );
            float size = HandleUtility.GetHandleSize( worldPosition ) * HandleSize;

            Handles.color = HandleColor;
            EditorGUI.BeginChangeCheck();
            Vector3 movedPosition = Handles.FreeMoveHandle( worldPosition, size, Vector3.zero, Handles.DotHandleCap );
            if ( !EditorGUI.EndChangeCheck() )
            {
                return;
            }

            Vector3 localPosition = transform.InverseTransformPoint( movedPosition );
            Undo.RecordObject( spriteRenderer, UndoName );
            spriteRenderer.SetCorner( corner, new Vector2( localPosition.x, localPosition.y ) );
            spriteRenderer.Rebuild();
            EditorUtility.SetDirty( spriteRenderer );
        }

        private void DrawQuadPreview( PerspectiveSpriteRenderer spriteRenderer, Transform transform )
        {
            for ( int i = 1; i < GridLineCount; i++ )
            {
                float t = ( float ) i / GridLineCount;
                Handles.color = GridColor;
                Handles.DrawLine( ToWorld( spriteRenderer, transform, new Vector2( t, 0f ) ), ToWorld( spriteRenderer, transform, new Vector2( t, 1f ) ) );
                Handles.DrawLine( ToWorld( spriteRenderer, transform, new Vector2( 0f, t ) ), ToWorld( spriteRenderer, transform, new Vector2( 1f, t ) ) );
            }

            for ( int i = 0; i < Corners.Length; i++ )
            {
                _outline[ i ] = transform.TransformPoint( spriteRenderer.GetCorner( Corners[ i ] ) );
            }

            _outline[ Corners.Length ] = _outline[ 0 ];
            Handles.color = OutlineColor;
            Handles.DrawAAPolyLine( OutlineWidth, _outline );
        }

        private Vector3 ToWorld( PerspectiveSpriteRenderer spriteRenderer, Transform transform, Vector2 normalized )
        {
            return transform.TransformPoint( spriteRenderer.Evaluate( normalized ) );
        }

        private void DrawSorting()
        {
            EditorGUILayout.LabelField( "Sorting", EditorStyles.boldLabel );

            MeshRenderer meshRenderer = ( ( PerspectiveSpriteRenderer ) target ).MeshRenderer;
            if ( meshRenderer == null )
            {
                return;
            }

            CacheSortingLayers();

            int currentIndex = System.Array.IndexOf( _sortingLayerIds, meshRenderer.sortingLayerID );
            EditorGUI.BeginChangeCheck();
            int layerIndex = EditorGUILayout.Popup( "Sorting Layer", Mathf.Max( currentIndex, 0 ), _sortingLayerNames );
            int order = EditorGUILayout.IntField( "Order In Layer", meshRenderer.sortingOrder );
            if ( !EditorGUI.EndChangeCheck() )
            {
                return;
            }

            foreach ( Object selected in targets )
            {
                MeshRenderer selectedRenderer = ( ( PerspectiveSpriteRenderer ) selected ).MeshRenderer;
                Undo.RecordObject( selectedRenderer, "Change Sorting" );
                selectedRenderer.sortingLayerID = _sortingLayerIds[ layerIndex ];
                selectedRenderer.sortingOrder = order;
                EditorUtility.SetDirty( selectedRenderer );
            }
        }

        private void DrawCornerTools()
        {
            EditorGUILayout.LabelField( "Corners", EditorStyles.boldLabel );

            bool editCorners = GUILayout.Toggle( _editCorners, "Edit Corners In Scene", EditorStyles.miniButton );
            if ( editCorners != _editCorners )
            {
                _editCorners = editCorners;
                SceneView.RepaintAll();
            }

            if ( GUILayout.Button( "Reset To Sprite Rect" ) )
            {
                foreach ( Object selected in targets )
                {
                    PerspectiveSpriteRenderer spriteRenderer = ( PerspectiveSpriteRenderer ) selected;
                    Undo.RecordObject( spriteRenderer, "Reset Perspective Sprite Corners" );
                    spriteRenderer.ResetCornersToSprite();
                    spriteRenderer.Rebuild();
                    EditorUtility.SetDirty( spriteRenderer );
                }

                SceneView.RepaintAll();
            }
        }

        private void CacheSortingLayers()
        {
            SortingLayer[] layers = SortingLayer.layers;
            if ( _sortingLayerIds != null && _sortingLayerIds.Length == layers.Length )
            {
                return;
            }

            _sortingLayerNames = new string[ layers.Length ];
            _sortingLayerIds = new int[ layers.Length ];

            for ( int i = 0; i < layers.Length; i++ )
            {
                _sortingLayerNames[ i ] = layers[ i ].name;
                _sortingLayerIds[ i ] = layers[ i ].id;
            }
        }
    }
}
