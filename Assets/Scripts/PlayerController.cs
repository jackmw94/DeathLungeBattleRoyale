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

    [SerializeField] private MeshRenderer m_playerColourIndicator;
    [SerializeField] private MeshRenderer m_empireColourIndicator;

    [SerializeField] private GameObject[] m_prestige;

    [SerializeField] private float m_height = 0.4f;
    [SerializeField] private float m_moveDuration = 0.5f;
    [SerializeField] private float m_distanceToKick = 0.4f;
    [SerializeField] private float m_startDistance = 15f;

    [SerializeField] private PlayerData m_playerData = new PlayerData();

    private bool m_movementDisabledUntilDataUpdated = false;
    private bool m_hasSetData = false;
    private Transform m_transform;

    public PlayerData PlayerData => m_playerData;

    private int PlayerId => m_playerData.PlayerId;
    private int EmpireId => m_playerData.EmpireId;
    private int Rank => m_playerData.Rank;
    private int AllowedMoves => m_playerData.AllowedMovements;
    private Vector3 Position => m_transform.position;

    private static int PlayerCount = 0;

    private void Start()
    {
        PlayerCount++;
        m_transform = transform;
        m_transform.position = new Vector3( m_transform.position.x, m_height, m_transform.position.z );

        if ( m_networkIdentity.isLocalPlayer )
        {
            CameraController.SetFollowTarget( m_transform );
            m_playerInput.PerformAction += PerformAction;
            CmdRegisterPlayer();
        }
        else
        {
            Destroy( m_playerInput );
        }

        m_moveIndicator.SetMoves( 0 );
    }

    private void OnDestroy()
    {
        PlayerCount--;
        if ( m_playerInput )
        {
            m_playerInput.PerformAction -= PerformAction;
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

    private void PerformAction( bool move, Vector2 movement )
    {
        Debug.Assert( AllowedMoves > 0, "Zero moves left!" );

        if ( !m_movementDisabledUntilDataUpdated && AllowedMoves > 0 )
        {
            if ( move )
            {
                StartCoroutine( MoveGradually( new Vector3( movement.x, 0f, movement.y ) ) );
                m_movementDisabledUntilDataUpdated = true;
                CmdMoveTaken( false );
            }
            else
            {
                var from = new Vector2( m_transform.position.x, m_transform.position.z );
                var to = new Vector2( m_transform.position.x + movement.x, m_transform.position.z + movement.y );
                if ( RouteInvolvesKick( EmpireId, from, to, out var player ) )
                {
                    CmdKickPlayer( player.PlayerId );
                    m_movementDisabledUntilDataUpdated = true;
                    CmdMoveTaken( true );
                }
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

        m_playerInput.enabled = AllowedMoves > 0;
    }

    private bool RouteInvolvesKick( int playerEmpireId, Vector2 from, Vector2 to, out PlayerController kickedPlayer )
    {
        // replace with raycast solution that looks for obstacles too

        var players = FindObjectsOfType<PlayerController>();
        foreach ( var player in players )
        {
            if ( player.EmpireId != playerEmpireId )
            {
                Vector2 playerPos = new Vector2( player.Position.x, player.Position.z );
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

    private static (float, Vector2) DistanceFromPointToLine( Vector2 lineStart, Vector2 lineEnd, Vector2 point )
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