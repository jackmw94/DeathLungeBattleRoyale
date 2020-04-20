using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public partial class PlayerController : NetworkBehaviour
{
    public const int MaxMoves = 6;

    [SerializeField] private NetworkIdentity m_networkIdentity;
    [SerializeField] private PlayerInput m_playerInput;
    [SerializeField] private MoveIndicator m_moveIndicator;
    [SerializeField] private MoveRequestUI m_moveRequestUi;
    [SerializeField] private FightUI m_fightUi;

    [SerializeField] private ColourContainer m_colours;
    [SerializeField] private GameObject m_playerIndicatorObject;

    [SerializeField] private float m_height = 0.4f;
    [SerializeField] private float m_moveDuration = 0.5f;
    [SerializeField] private float m_distanceToKick = 0.4f;
    [SerializeField] private float m_startDistance = 15f;

    [SerializeField, SyncVar( hook = "UpdatePlayerId" )] private int m_playerId = 0;
    [SerializeField] private int m_empireId = -1;

    [SerializeField, SyncVar( hook = "UpdateEmpireRank" )] private int m_rank = 0;

    [SerializeField, SyncVar( hook = "UpdateAllowedMovements" )] private int m_allowedMovements = 0;

    public int PlayerId => m_playerId;
    public int EmpireId => m_empireId;
    public int Rank => m_rank;
    public int AllowedMoves => m_allowedMovements;
    public Vector3 Position => m_transform.position;

    private Material m_playerColourIndicator;
    private Transform m_transform;

    public static int PlayerCount = 0;

    private void Start()
    {
        PlayerCount++;
        m_transform = transform;
        m_transform.position = new Vector3( m_transform.position.x, m_height, m_transform.position.z );

        if ( m_networkIdentity.isLocalPlayer )
        {
            CameraController.SetFollowTarget( m_transform );
            m_playerInput.PerformAction += PerformAction;
            CmdSetPlayerIndex();
        }
        else
        {
            UpdatePlayerId( m_playerId );
            Destroy( m_playerInput );
        }

        m_moveIndicator.SetMoves( m_allowedMovements );
    }

    private void OnDestroy()
    {
        PlayerCount--;
        if ( m_playerInput )
        {
            m_playerInput.PerformAction -= PerformAction;
        }
    }

    private void Update()
    {
        if ( m_networkIdentity.isLocalPlayer )
        {
            if ( Input.GetKeyDown( KeyCode.U ) )
            {
                CmdSetAllowedMoves( m_allowedMovements + 2 );
                m_moveIndicator.SetMoves( m_allowedMovements );
            }

            m_playerInput.enabled = m_allowedMovements > 0;
        }
    }
    
    private void UpdatePlayerId( int value )
    {
        Debug.Log( $"Updating player Id on {gameObject.name} to {value}" );
        gameObject.name = $"Player {value}";
        m_playerId = value;
        m_playerColourIndicator = m_playerIndicatorObject.GetComponent<MeshRenderer>().material;
        m_playerColourIndicator.color = m_colours.GetColour( m_playerId );

        if ( m_networkIdentity.isLocalPlayer )
        {
            m_transform.position = GetStartPosition( m_playerId );
        }
    }
    
    private void RespondToMoveRequest( int move )
    {
        CmdRespondToMoveRequest( move );
    }

    private void RespondToFightRequest( FightUI.FightChoice fightResponse )
    {
        CmdRespondToFightRequest( fightResponse );
    }
    
    private void UpdateEmpireRank( int value )
    {
        m_rank = value;
    }

    private void UpdateAllowedMovements( int value )
    {
        m_allowedMovements = value;
        m_moveIndicator.SetMoves( m_allowedMovements );
    }

    private void PerformAction( bool move, Vector2 movement )
    {
        Debug.Assert( m_allowedMovements > 0, "Zero moves left!" );

        if ( move )
        {
            StartCoroutine( MoveGradually( new Vector3( movement.x, 0f, movement.y ) ) );
            CmdSetAllowedMoves( m_allowedMovements - 1 );
        }
        else
        {
            var from = new Vector2( m_transform.position.x, m_transform.position.z );
            var to = new Vector2( m_transform.position.x + movement.x, m_transform.position.z + movement.y );
            if ( RouteInvolvesKick( m_empireId, from, to, out var player ) )
            {
                CmdKickPlayer( player.PlayerId );
                CmdSetAllowedMoves( 0 );
            }
        }
    }

    private IEnumerator MoveGradually( Vector3 movement )
    {
        m_playerInput.enabled = false;
        Vector3 startPos = m_transform.position;
        Vector3 endPos = m_transform.position + movement;

        var startTime = Time.time;
        float fract = 0f;

        do
        {
            yield return null;
            fract = ( Time.time - startTime ) / m_moveDuration;
            m_transform.position = Vector3.Lerp( startPos, endPos, fract );
        } while ( fract < 1f );

        m_playerInput.enabled = true;
    }

    public bool RouteInvolvesKick( int playerEmpireId, Vector2 from, Vector2 to, out PlayerController kickedPlayer )
    {
        var players = FindObjectsOfType<PlayerController>();
        for ( int i = 0; i < players.Length; i++ )
        {
            var player = players[i];
            if ( player.EmpireId != playerEmpireId )
            {
                Vector2 playerPos = new Vector2(player.Position.x, player.Position.z);
                (float f, Vector2 p) = DistanceFromPointToLine( from, to, playerPos );
                Debug.Log( $"Distance from line ({from} -> {to}) to player {player.gameObject.name} (at {playerPos}) is {f}. Closest point is {p}" );
                if ( f <= m_distanceToKick )
                {
                    kickedPlayer = player;
                    return true;
                }
            }
        }

        kickedPlayer = null;
        return false;
    }

    public static (float, Vector2) DistanceFromPointToLine( Vector2 lineStart, Vector2 lineEnd, Vector2 point )
    {
        Vector2 closestPoint = Vector2.zero;

        float xDistanceToStart = point.x - lineStart.x; // getting these vectors in origin-space, removing relativity
        float yDistanceToStart = point.y - lineStart.y;
        float xDistanceToEnd = lineEnd.x - lineStart.x;
        float yDistanceToEnd = lineEnd.y - lineStart.y;

        float dotProduct = xDistanceToStart * xDistanceToEnd + yDistanceToStart * yDistanceToEnd;
        var lenSq = xDistanceToEnd * xDistanceToEnd + yDistanceToEnd * yDistanceToEnd;
        var lineFraction = -1f;
        if ( Mathf.Abs( lenSq ) > 0.0000001f )
        {
            //in case of 0 length line
            lineFraction = dotProduct / lenSq;
        }

        if ( lineFraction < 0 )
        {
            closestPoint.x = lineStart.x;
            closestPoint.y = lineStart.y;
        }
        else if ( lineFraction > 1 )
        {
            closestPoint.x = lineEnd.x;
            closestPoint.y = lineEnd.y;
        }
        else
        {
            closestPoint.x = lineStart.x + lineFraction * xDistanceToEnd;
            closestPoint.y = lineStart.y + lineFraction * yDistanceToEnd;
        }

        var dx = point.x - closestPoint.x;
        var dy = point.y - closestPoint.y;
        float dist = Mathf.Sqrt( dx * dx + dy * dy );

        return (dist, closestPoint);
    }

    private Vector3 GetStartPosition( int playerNumber )
    {
        var order = playerNumber % 4;
        bool secondSet = playerNumber >= 4;

        Vector2 offset = Vector2.up * m_startDistance;
        offset = MoveIndicator.Rotate( offset, order * 90 + ( secondSet ? 45 : 0 ) );

        return new Vector3( offset.x, 0f, offset.y );
    }
}

[Serializable]
public class PlayerData
{
    public int PlayerId;
    public int EmpireId;
    public int Rank;
    public int AllowedMovements;
}