using System;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class PlayerInput : NetworkBehaviour
{
    [SerializeField] private NetworkIdentity m_networkIdentity;

    [SerializeField] private Color m_moveColour;
    [SerializeField] private Color m_kickColour;
    [SerializeField] private Color m_badActionColour;
    [SerializeField] private LineRenderer m_line;

    [SerializeField] private float m_minStepFraction = 0.2f;
    [SerializeField] private float m_maxStepSize;
    [SerializeField] private Vector2 m_movementVect;

    public Action<bool, Vector2> PerformAction = ( move, moveVector ) => { };

    private bool m_hitsTerrain = false;
    private bool m_hasValue = false;
    private Transform m_transform;

    private void Start()
    {
        m_transform = transform;
    }

    private void OnDisable()
    {
        m_hasValue = false;
        m_line.enabled = false;
    }

    private void Update()
    {
        if ( Input.GetMouseButton( 0 ) )
        {
            Vector2 groundPos = GroundPositionFromScreenPoint( Camera.main, Input.mousePosition, 0f, out bool _ );
            Vector2 currentPos = new Vector2( m_transform.position.x, m_transform.position.z );

            m_movementVect = groundPos - currentPos;

            if ( m_movementVect.magnitude > m_maxStepSize )
            {
                m_movementVect = m_movementVect.normalized * m_maxStepSize;
                groundPos = currentPos + m_movementVect;
            }

            Vector3 startPos = new Vector3( m_transform.position.x, m_line.GetPosition( 0 ).y, m_transform.position.z );
            Vector3 endPos = new Vector3( groundPos.x, m_line.GetPosition( 1 ).y, groundPos.y );

            m_line.SetPosition( 0, startPos );
            m_line.SetPosition( 1, endPos );

            var rayDirection = endPos - startPos;
            SetHitsTerrainFlag( rayDirection );

            m_line.startColor = m_hitsTerrain ? m_badActionColour : m_moveColour;
            m_line.endColor = m_hitsTerrain ? m_badActionColour : m_moveColour;

            float movementMagnitude = m_movementVect.magnitude / m_maxStepSize;
            m_hasValue = movementMagnitude > m_minStepFraction;
        }
        else if ( Input.GetMouseButtonUp( 0 ) )
        {
            float movementMagnitude = m_movementVect.magnitude / m_maxStepSize;
            m_hasValue = movementMagnitude > m_minStepFraction;
        }


        m_line.enabled = m_hasValue;

        if ( m_hasValue && !m_hitsTerrain )
        {
            if ( Input.GetKeyDown( KeyCode.M ) )
            {
                PerformAction( true, m_movementVect );
                m_hasValue = false;
            }
            else if ( Input.GetKeyDown( KeyCode.K ) )
            {
                PerformAction( false, m_movementVect );
                m_hasValue = false;
            }
        }
    }


    private void SetHitsTerrainFlag( Vector3 rayDirection )
    {
        m_hitsTerrain = false;
        Ray ray = new Ray( transform.position, rayDirection );
        if ( Physics.Raycast( ray, rayDirection.magnitude + 0.5f, LayerMask.GetMask( "Terrain" ) ) )
        {
            m_hitsTerrain = true;
            return;
        }

        Vector3 rotatedVector = ( Quaternion.AngleAxis( 90, Vector3.up ) * rayDirection ).normalized * 0.5f;
        ray = new Ray( transform.position + rotatedVector, rayDirection );
        if ( Physics.Raycast( ray, rayDirection.magnitude, LayerMask.GetMask( "Terrain" ) ) )
        {
            m_hitsTerrain = true;
            return;
        }

        Vector3 invRotatedVector = ( Quaternion.AngleAxis( -90, Vector3.up ) * rayDirection ).normalized * 0.5f;
        ray = new Ray( transform.position + invRotatedVector, rayDirection );
        if ( Physics.Raycast( ray, rayDirection.magnitude, LayerMask.GetMask( "Terrain" ) ) )
        {
            m_hitsTerrain = true;
            return;
        }

        ray = new Ray( transform.position + rotatedVector + rayDirection, invRotatedVector );
        if ( Physics.Raycast( ray, 1f, LayerMask.GetMask( "Terrain" ) ) )
        {
            m_hitsTerrain = true;
        }
    }

    private static Vector2 GroundPositionFromScreenPoint( Camera camera, Vector2 mousePosition, float targetHeight, out bool success )
    {
        var ray = camera.ScreenPointToRay( mousePosition );
        var hPlane = new Plane( Vector3.up, Vector3.up * targetHeight );

        if ( hPlane.Raycast( ray, out var distance ) )
        {
            var groundPos = ray.GetPoint( distance );
            success = true;
            return new Vector2( groundPos.x, groundPos.z );
        }

        success = false;
        Debug.LogError( "[PlayerInput] GroundPositionFromScreenPoint : Cannot find position on ground plane" );

        return Vector2.zero;
    }
}