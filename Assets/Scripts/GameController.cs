using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class GameController : NetworkBehaviour
{
    private enum GameState
    {
        RequestMoveCount,
        EmpiresMoving,
        PlayerBattle
    }

    private class BattleState
    {
        public PlayerController Attacker;
        public PlayerController Defender;
        public FightUI.FightChoice AttackerChoice = FightUI.FightChoice.None;
        public FightUI.FightChoice DefenderChoice = FightUI.FightChoice.None;
    }

    [SerializeField] private Text m_gameInfoDisplay;

    [SerializeField] private List<PlayerController> m_allPlayers = new List<PlayerController>();

    private readonly Queue<(PlayerController, PlayerController)> m_battleQueue = new Queue<(PlayerController, PlayerController)>();
    private readonly Dictionary<int, List<PlayerController>> m_playersByEmpire = new Dictionary<int, List<PlayerController>>();

    private readonly Dictionary<PlayerController, int> m_requestMovesFromSet = new Dictionary<PlayerController, int>();
    private int m_moveResponses = 0;

    private GameState m_gameState;
    private int m_playerCount = 0;
    private BattleState m_battleTakingPlace = null;

    private bool m_gameInfoTextChanged = false;
    private string m_gameInfo = "";

    private bool m_startedGame = false;

    private static GameController m_instance;
    public static GameController Instance => m_instance;

    private void Start()
    {
        m_instance = this;
    }

    private void Update()
    {
        if ( Input.GetKeyDown( KeyCode.G ) )
        {
            if ( !m_startedGame )
            {
                m_startedGame = true;
                StartCoroutine( GameLoop() );
            }
        }

        if ( m_gameInfoTextChanged )
        {
            m_gameInfoTextChanged = false;
            m_gameInfoDisplay.text = m_gameInfo;
        }

        
    }

    private IEnumerator GameLoop()
    {
        Debug.Log( $"Starting game loop!" );
        while ( true )
        {
            // get all empire leaders
            m_requestMovesFromSet.Clear();
            foreach ( KeyValuePair<int, List<PlayerController>> keyValuePair in m_playersByEmpire )
            {
                bool foundLeader = false;
                foreach ( PlayerController playerController in keyValuePair.Value )
                {
                    if ( playerController.Rank == 0 )
                    {
                        Debug.AssertFormat( !foundLeader, "Found multiple leaders in empire of ID {0}", keyValuePair.Key );
                        m_requestMovesFromSet.Add( playerController, -1 );
                        foundLeader = true;
                    }
                }
            }
            m_moveResponses = 0;

            string info = "Requesting moves from ";
            // request moves from each
            foreach ( KeyValuePair<PlayerController, int> keyValuePair in m_requestMovesFromSet )
            {
                info = $"{info} {keyValuePair.Key.PlayerId},";
                var player = keyValuePair.Key;
                player.RpcRequestMoves();
            }
            CmdSetGameInfo( info );

            Debug.Log( $"Sent all requests" );

            int tempMoveResponses = 0;
            // wait until we have all returned
            while ( m_moveResponses < m_requestMovesFromSet.Count )
            {
                if ( m_moveResponses != tempMoveResponses )
                {
                    info = "Still waiting for moves from ";
                    foreach ( KeyValuePair<PlayerController, int> keyValuePair in m_requestMovesFromSet )
                    {
                        if ( keyValuePair.Value == -1 )
                        {
                            info = $"{info} {keyValuePair.Key.PlayerId},";
                        }
                    }
                    CmdSetGameInfo( info );
                    tempMoveResponses = m_moveResponses;
                }
                // tell players we're waiting for moves
                yield return null;
            }

            Debug.Log( $"Finished waiting for move choices" );

            // show all players move counts

            // remove duplicates / run move policy
            int[] moves = new int[PlayerController.MaxMoves];
            foreach ( int moveChoice in m_requestMovesFromSet.Values )
            {
                Debug.Log( $"Filtering player who chose move {moveChoice}" );
                moves[moveChoice - 1]++;
            }

            // get movement order
            for ( int i = 1; i <= PlayerController.MaxMoves; i++ )
            {
                Debug.Log( $"Filtering move choices : moves[{i - 1}] (for choice {i}) has count of {moves[i - 1]}" );
                if ( moves[i - 1] == 1 )
                {
                    foreach ( KeyValuePair<PlayerController, int> keyValuePair in m_requestMovesFromSet )
                    {
                        if ( keyValuePair.Value == i )
                        {
                            Debug.Log( $"Updating allowed move to {i} for empire with id {keyValuePair.Key.EmpireId}" );
                            // send moves to all empire in rank order
                            CmdSetGameInfo( $"Empire #{keyValuePair.Key.EmpireId} is moving" );
                            yield return UpdateAllowedMovesForEmpire( keyValuePair.Key.EmpireId, i );
                            Debug.Log( $"Returned from doing moves for empire {keyValuePair.Key.EmpireId}" );
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSecondsRealtime( 1.5f );

            // check for end game state
            // if not then loop
        }
    }

    [Command]
    private void CmdSetGameInfo( string s )
    {
        RpcSetGameInfo( s );
    }

    [ClientRpc]
    private void RpcSetGameInfo( string s )
    {
        m_gameInfoTextChanged = true;
        m_gameInfo = s;
    }

    private IEnumerator UpdateAllowedMovesForEmpire( int empireId, int moveCount )
    {
        var players = m_playersByEmpire[empireId];
        List<PlayerController> waitingForPlayers = new List<PlayerController>();

        var orderedPlayers = players.OrderBy( p => p.Rank ).ToArray();
        for ( int i = 0; i < orderedPlayers.Length; i++ )
        {
            var player = orderedPlayers[i];
            var rank = player.Rank;
            waitingForPlayers.Add( player );
            player.CmdSetAllowedMoves( moveCount );

            if ( i < orderedPlayers.Length - 1 )
            {
                if ( orderedPlayers[i + 1].Rank != rank )
                {
                    Debug.Log( $"Waiting for {waitingForPlayers.Count} players" );
                    // end of rank group, wait until they all done
                    yield return WaitUntilNoMovesAndCheckForBattles( waitingForPlayers );
                    Debug.Log( $"Finished waiting" );
                    waitingForPlayers.Clear();
                }
            }
            else
            {
                Debug.Log( $"Waiting for {waitingForPlayers.Count} players - final" );
                // end of rank group, wait until they all done
                yield return WaitUntilNoMovesAndCheckForBattles( waitingForPlayers );
                Debug.Log( $"Finished waiting - final" );
            }
        }
    }

    private IEnumerator WaitUntilNoMovesAndCheckForBattles( List<PlayerController> players )
    {
        Debug.Log( $"Starting waiting for {players.Count} players" );
        bool finished = true;
        do
        {
            yield return null;

            // do we have a battle in queue ? 
            if ( m_battleQueue.Count > 0 )
            {
                Debug.Log( "Found a battle!" );
                var (attacker, defender) = m_battleQueue.Dequeue();
                m_battleTakingPlace = new BattleState { Attacker = attacker, Defender = defender };
                CmdSetGameInfo( $"Battle happening between players {attacker.PlayerId} and {defender.PlayerId}" );
                yield return HandleBattle( attacker, defender );
                m_battleTakingPlace = null;
                continue;
            }

            // are all players done ?
            finished = true;
            foreach ( PlayerController player in players )
            {
                if ( player.AllowedMoves > 0 )
                {
                    finished = false;
                }
            }
        } while ( !finished );

        Debug.Log( $"Finished waiting for moves / battles" );
    }

    private IEnumerator HandleBattle( PlayerController attacker, PlayerController defender )
    {
        attacker.RpcRequestFightResponse();
        defender.RpcRequestFightResponse();
        while ( m_battleTakingPlace.AttackerChoice == FightUI.FightChoice.None || m_battleTakingPlace.DefenderChoice == FightUI.FightChoice.None )
        {
            yield return null;
        }
        ResolveFight();
        yield return new WaitForSecondsRealtime( 3f );
    }

    private void ResolveFight()
    {
        string info = "";
        switch ( m_battleTakingPlace.AttackerChoice )
        {
            case FightUI.FightChoice.Rock:
                switch ( m_battleTakingPlace.DefenderChoice )
                {
                    case FightUI.FightChoice.Rock:
                        // draw
                        info = "Fight was a draw, both players chose rock";
                        break;
                    case FightUI.FightChoice.Paper:
                        info = "Defender won, paper beats rock";
                        m_battleTakingPlace.Attacker.RpcResetPosition();
                        break;
                    case FightUI.FightChoice.Scissors:
                        info = "Attacker won, rock beats scissors";
                        SetPlayerEmpire( m_battleTakingPlace.Defender, m_battleTakingPlace.Attacker.EmpireId );
                        break;
                }
                break;
            case FightUI.FightChoice.Paper:
                switch ( m_battleTakingPlace.DefenderChoice )
                {
                    case FightUI.FightChoice.Rock:
                        info = "Attacker won, paper beats rock";
                        SetPlayerEmpire( m_battleTakingPlace.Defender, m_battleTakingPlace.Attacker.EmpireId );
                        break;
                    case FightUI.FightChoice.Paper:
                        // draw
                        info = "Fight was a draw, both players chose paper";
                        break;
                    case FightUI.FightChoice.Scissors:
                        info = "Defender won, scissors beats paper";
                        m_battleTakingPlace.Attacker.RpcResetPosition();
                        break;
                }
                break;
            case FightUI.FightChoice.Scissors:
                switch ( m_battleTakingPlace.DefenderChoice )
                {
                    case FightUI.FightChoice.Rock:
                        info = "Defender won, rock beats scissors";
                        m_battleTakingPlace.Attacker.RpcResetPosition();
                        break;
                    case FightUI.FightChoice.Paper:
                        info = "Attacker won, scissors beats paper";
                        SetPlayerEmpire( m_battleTakingPlace.Defender, m_battleTakingPlace.Attacker.EmpireId );
                        break;
                    case FightUI.FightChoice.Scissors:
                        // draw
                        info = "Fight was a draw, both players chose scissors";
                        break;
                }
                break;
        }

        CmdSetGameInfo( info );
    }

    public void SubmitMoveCount( PlayerController player, int moves )
    {
        bool hasPlayer = m_requestMovesFromSet.ContainsKey( player );
        Debug.Assert( hasPlayer, "Could not find player in request moves set" );
        if ( hasPlayer )
        {
            m_requestMovesFromSet[player] = moves;
            m_moveResponses++;
            Debug.Log( $"Responding to move request {m_moveResponses} of {m_requestMovesFromSet.Count} players have responded" );
        }
    }

    public void SubmitFightChoice( PlayerController player, FightUI.FightChoice fight )
    {
        if ( m_battleTakingPlace.Attacker.PlayerId == player.PlayerId )
        {
            m_battleTakingPlace.AttackerChoice = fight;
            return;
        }
        if ( m_battleTakingPlace.Defender.PlayerId == player.PlayerId )
        {
            m_battleTakingPlace.DefenderChoice = fight;
            return;
        }
        Debug.LogError( $"Could not find a player with id={player.PlayerId} in current battle" );
    }

    public int RegisterPlayer( PlayerController player )
    {
        m_allPlayers.Add( player );
        m_playerCount++;
        SetPlayerEmpire( player, m_playerCount - 1 );
        return m_playerCount - 1;
    }

    private void SetPlayerEmpire( PlayerController player, int empireId )
    {
        // try remove
        if ( m_playersByEmpire.ContainsKey( player.EmpireId ) )
        {
            var empireSet = m_playersByEmpire[player.EmpireId];
            if ( empireSet.Contains( player ) )
            {
                empireSet.Remove( player );
            }
            UpdateEmpireAndRankIds( empireSet, player.EmpireId );
        }

        // try add
        if ( !m_playersByEmpire.ContainsKey( empireId ) )
        {
            m_playersByEmpire.Add( empireId, new List<PlayerController>() );
        }

        // if we want to insert winners into certain ranks, we should do that here

        var empire = m_playersByEmpire[empireId];
        empire.Add( player );
        UpdateEmpireAndRankIds( empire, empireId );
    }

    private void UpdateEmpireAndRankIds( List<PlayerController> inEmpire, int empireId )
    {
        for ( int i = 0; i < inEmpire.Count; i++ )
        {
            inEmpire[i].CmdSetPlayerRank( i );
            inEmpire[i].CmdSetEmpire( empireId );
        }
    }

    public void QueueBattle( PlayerController attacker, int defenderId )
    {
        var defender = m_allPlayers[defenderId];
        m_battleQueue.Enqueue( (attacker, defender) );
    }
}