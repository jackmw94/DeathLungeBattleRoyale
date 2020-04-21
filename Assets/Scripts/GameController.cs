using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class GameController : NetworkBehaviour
{
    // want to change this based on design choice
    private const int MaxRank = 2;

    private class BattleState
    {
        public int AttackerId;
        public int DefenderId;
        public FightUI.FightChoice AttackerChoice = FightUI.FightChoice.None;
        public FightUI.FightChoice DefenderChoice = FightUI.FightChoice.None;
    }

    [SerializeField] private DesignChoices m_designChoices;
    [SerializeField] private GameObject m_designChoiceUIRoot;
    [SerializeField] private GameObject m_mazeRoot;
    [SerializeField] private Text m_gameInfoDisplay;

    [SerializeField] private List<PlayerController> m_allPlayers = new List<PlayerController>();
    [SerializeField] private List<PlayerData> m_allPlayersData = new List<PlayerData>();

    private readonly Queue<(int, int)> m_battleQueue = new Queue<(int, int)>();
    private readonly Dictionary<int, List<int>> m_playersByEmpire = new Dictionary<int, List<int>>();

    // key is player id, value is requested number of moves
    private readonly Dictionary<int, int> m_requestMovesFromSet = new Dictionary<int, int>();
    private int m_moveResponses = 0;

    private int m_playerCount = 0;
    private BattleState m_battleTakingPlace = null;

    private bool m_initialisedServer = false;
    private bool m_gameInfoTextChanged = false;
    private string m_gameInfo = "";

    private bool m_startedGame = false;

    public static GameController Instance { get; private set; }
    public static DesignChoices DesignChoices { get; private set; }

    private void Start()
    {
        Instance = this;
        DesignChoices = m_designChoices;
    }

    private void OnDisable()
    {
        Debug.Log( $"GameController disabling - should be at end of hosted game" );
        m_initialisedServer = false;
        //m_designChoiceUIRoot.SetActive( false );
    }

    private void Update()
    {
        if ( !m_startedGame && isServer )
        {
            if ( Input.GetKeyDown( KeyCode.G ) )
            {
                //m_designChoiceUIRoot.SetActive( false );
                m_startedGame = true;
                StartCoroutine( GameLoop() );
            }

            if ( !m_initialisedServer )
            {
                m_initialisedServer = true;
                //m_designChoiceUIRoot.SetActive( true );
                CmdSetGameInfoForServerAndClient( $"Press G to start the game ({m_playerCount} players)", $"Waiting for host to start the game ({m_playerCount} players)");
            }
        }

        if ( m_gameInfoTextChanged )
        {
            m_gameInfoTextChanged = false;
            m_gameInfoDisplay.text = m_gameInfo;
        }
    }

    private void UpdateAllPlayerData()
    {
        Debug.Log( $"Updating all players' data" );
        for ( int i = 0; i < m_allPlayers.Count; i++ )
        {
            var player = m_allPlayers[i];
            var data = m_allPlayersData[i];
            var serialized = data.Serialize();
            Debug.Log( $"Sending serialized data : {serialized}" );
            player.RpcUpdatePlayerData( serialized );
        }
    }

    private void UpdatePlayerData( int playerId )
    {
        Debug.Log( $"Updating player id {playerId}'s data" );
        var player = m_allPlayers[playerId];
        var data = m_allPlayersData[playerId];
        var serialized = data.Serialize();
        Debug.Log( $"Sending serialized data : {serialized}" );
        player.RpcUpdatePlayerData( serialized );
    }

    private IEnumerator GameLoop()
    {
        Debug.Log( $"Starting game loop!" );
        while ( true )
        {
            RequestMoves();
            Debug.Log( $"Sent all requests" );

            yield return WaitForMoveResponses();
            Debug.Log( $"Finished waiting for move choices" );

            // show all players move counts ?

            // remove duplicates / run move policy
            int[] moves = new int[PlayerController.MaxMoves];
            foreach ( int moveChoice in m_requestMovesFromSet.Values )
            {
                Debug.Log( $"Filtering player who chose move {moveChoice}" );
                moves[moveChoice - 1]++;
            }

            // get movement order
            yield return RunPlayerMovement( moves );

            // wait just for nicer flow
            yield return new WaitForSecondsRealtime( 1.5f );

            // check for end game state
            // if not then loop
        }
    }

    public void RegisterPlayer( PlayerController player )
    {
        var playerId = m_playerCount;

        m_allPlayers.Add( player );
        m_allPlayersData.Add( new PlayerData()
        {
            PlayerId = playerId,
            EmpireId = playerId,
            Rank = 0,
            AllowedMovements = 0
        } );

        SetPlayerEmpire( playerId, playerId );
        UpdateAllPlayerData();
        
        m_playerCount++;

        string plural = m_playerCount == 1 ? "" : "s";
        CmdSetGameInfoForServerAndClient( $"Press G to start the game ({m_playerCount} player{plural})", $"Waiting for host to start the game ({m_playerCount} player{plural})" );
    }

    private void RequestMoves()
    {
        // get all empire leaders
        m_requestMovesFromSet.Clear();
        foreach ( KeyValuePair<int, List<int>> keyValuePair in m_playersByEmpire )
        {
            bool foundLeader = false;
            foreach ( int playerId in keyValuePair.Value )
            {
                var playerData = m_allPlayersData[playerId];
                if ( playerData.Rank == 0 )
                {
                    Debug.AssertFormat( !foundLeader, "Found multiple leaders in empire of ID {0}", keyValuePair.Key );
                    m_requestMovesFromSet.Add( playerData.PlayerId, -1 );
                    foundLeader = true;
                }
            }
        }
        m_moveResponses = 0;

        string info = "Requesting moves from ";
        // request moves from each
        foreach ( KeyValuePair<int, int> playerIdToNumMoves in m_requestMovesFromSet )
        {
            var playerId = playerIdToNumMoves.Key;
            info = $"{info} {playerId},";
            m_allPlayers[playerId].RpcRequestMoves();
        }
        CmdSetGameInfo( info );
    }

    public void SubmitMoveCount( int playerId, int moves )
    {
        bool hasPlayer = m_requestMovesFromSet.ContainsKey( playerId );
        Debug.Assert( hasPlayer, "Could not find player in request moves set" );
        if ( hasPlayer )
        {
            m_requestMovesFromSet[playerId] = moves;
            m_moveResponses++;
            Debug.Log( $"Responding to move request {m_moveResponses} of {m_requestMovesFromSet.Count} players have responded" );
        }
    }

    private IEnumerator WaitForMoveResponses()
    {
        int tempMoveResponses = 0;
        string info = "";

        // wait until we have all returned
        while ( m_moveResponses < m_requestMovesFromSet.Count )
        {
            if ( m_moveResponses != tempMoveResponses )
            {
                info = "Still waiting for moves from ";
                foreach ( KeyValuePair<int, int> playerIdToNumMoves in m_requestMovesFromSet )
                {
                    if ( playerIdToNumMoves.Value == -1 )
                    {
                        info = $"{info} {playerIdToNumMoves.Key},";
                    }
                }

                // tell players we're waiting for moves
                CmdSetGameInfo( info );
                tempMoveResponses = m_moveResponses;
            }
            yield return null;
        }
    }

    private IEnumerator RunPlayerMovement( int[] moves )
    {
        for ( int i = 1; i <= PlayerController.MaxMoves; i++ )
        {
            Debug.Log( $"Filtering move choices : moves[{i - 1}] (for choice {i}) has count of {moves[i - 1]}" );
            if ( moves[i - 1] == 1 )
            {
                foreach ( KeyValuePair<int, int> playerIdToNumMoves in m_requestMovesFromSet )
                {
                    var moveCount = playerIdToNumMoves.Value;
                    if ( moveCount == i )
                    {
                        var playerId = playerIdToNumMoves.Key;
                        var playerData = m_allPlayersData[playerId];
                        Debug.Log( $"Updating allowed moves to {i} for player with id {playerData.PlayerId} in empire with id {playerData.EmpireId}" );
                        // send moves to all empire in rank order
                        CmdSetGameInfo( $"Empire #{playerData.EmpireId} is moving" );
                        yield return SetAllowedNumberOfMovesForEmpire( playerData.EmpireId, i );
                        Debug.Log( $"Returned from doing moves for empire {playerData.EmpireId}" );
                        break;
                    }
                }
            }
        }
    }

    private IEnumerator SetAllowedNumberOfMovesForEmpire( int empireId, int moveCount )
    {
        var players = m_playersByEmpire[empireId];
        List<PlayerData> waitingForPlayers = new List<PlayerData>();

        var orderedPlayers = players.Select( p => m_allPlayersData[p] ).OrderBy( p => p.Rank ).ToArray();
        for ( int i = 0; i < orderedPlayers.Length; i++ )
        {
            var player = orderedPlayers[i];
            var rank = player.Rank;
            waitingForPlayers.Add( player );

            player.AllowedMovements = moveCount;
            UpdatePlayerData( player.PlayerId );

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

    private IEnumerator WaitUntilNoMovesAndCheckForBattles( List<PlayerData> players )
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
                m_battleTakingPlace = new BattleState { AttackerId = attacker, DefenderId = defender };
                CmdSetGameInfo( $"Battle happening between players {attacker} and {defender}" );
                yield return HandleBattle( attacker, defender );
                m_battleTakingPlace = null;
                continue;
            }

            // are all players done ?
            finished = true;
            foreach ( PlayerData player in players )
            {
                if ( player.AllowedMovements > 0 )
                {
                    finished = false;
                }
            }
        } while ( !finished );

        Debug.Log( $"Finished waiting for moves / battles" );
    }

    public void PlayerMoveTaken( int playerId, bool hasKicked )
    {
        var playerData = m_allPlayersData[playerId];
        playerData.AllowedMovements = hasKicked ? 0 : playerData.AllowedMovements - 1;
        UpdatePlayerData( playerId );
    }

    public void QueueBattle( int attackerId, int defenderId )
    {
        m_battleQueue.Enqueue( (attackerId, defenderId) );
    }

    private IEnumerator HandleBattle( int attackerId, int defenderId )
    {
        m_allPlayers[attackerId].RpcRequestFightResponse();
        m_allPlayers[defenderId].RpcRequestFightResponse();
        while ( m_battleTakingPlace.AttackerChoice == FightUI.FightChoice.None || m_battleTakingPlace.DefenderChoice == FightUI.FightChoice.None )
        {
            yield return null;
        }
        ResolveCurrentFight();
        yield return new WaitForSecondsRealtime( 3f );
    }

    public void SubmitFightChoice( int playerId, FightUI.FightChoice fight )
    {
        if ( m_battleTakingPlace.AttackerId == playerId )
        {
            m_battleTakingPlace.AttackerChoice = fight;
            return;
        }
        if ( m_battleTakingPlace.DefenderId == playerId )
        {
            m_battleTakingPlace.DefenderChoice = fight;
            return;
        }
        Debug.LogError( $"Could not find a player with id={playerId} in current battle" );
    }

    private void ResolveCurrentFight()
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
                        var attackerId = m_battleTakingPlace.AttackerId;
                        m_allPlayers[attackerId].RpcResetPosition();
                        break;
                    case FightUI.FightChoice.Scissors:
                        info = "Attacker won, rock beats scissors";
                        var attackerData = m_allPlayersData[m_battleTakingPlace.AttackerId];
                        SetPlayerEmpire( m_battleTakingPlace.DefenderId, attackerData.EmpireId );
                        break;
                }
                break;
            case FightUI.FightChoice.Paper:
                switch ( m_battleTakingPlace.DefenderChoice )
                {
                    case FightUI.FightChoice.Rock:
                        info = "Attacker won, paper beats rock";
                        var attackerData = m_allPlayersData[m_battleTakingPlace.AttackerId];
                        SetPlayerEmpire( m_battleTakingPlace.DefenderId, attackerData.EmpireId );
                        break;
                    case FightUI.FightChoice.Paper:
                        // draw
                        info = "Fight was a draw, both players chose paper";
                        break;
                    case FightUI.FightChoice.Scissors:
                        info = "Defender won, scissors beats paper";
                        var attackerId = m_battleTakingPlace.AttackerId;
                        m_allPlayers[attackerId].RpcResetPosition();
                        break;
                }
                break;
            case FightUI.FightChoice.Scissors:
                switch ( m_battleTakingPlace.DefenderChoice )
                {
                    case FightUI.FightChoice.Rock:
                        info = "Defender won, rock beats scissors";
                        var attackerId = m_battleTakingPlace.AttackerId;
                        m_allPlayers[attackerId].RpcResetPosition();
                        break;
                    case FightUI.FightChoice.Paper:
                        info = "Attacker won, scissors beats paper";
                        var attackerData = m_allPlayersData[m_battleTakingPlace.AttackerId];
                        SetPlayerEmpire( m_battleTakingPlace.DefenderId, attackerData.EmpireId );
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

    private void SetPlayerEmpire( int playerId, int empireId )
    {
        var playerData = m_allPlayersData[playerId];
        int previousEmpireId = playerData.EmpireId;
        
        // try remove
        if ( m_playersByEmpire.ContainsKey( playerData.EmpireId ) )
        {
            var empirePlayerIds = m_playersByEmpire[playerData.EmpireId];
            if ( empirePlayerIds.Contains( playerData.PlayerId ) )
            {
                empirePlayerIds.Remove( playerData.PlayerId );
            }

            int generalId = -1;
            for ( int i = 0; i < empirePlayerIds.Count; i++ )
            {
                var empirePlayer = m_allPlayersData[i];
                if ( i == 0 )
                {
                    generalId = empirePlayer.PlayerId;
                }

                empirePlayer.EmpireSize = empirePlayerIds.Count;
                empirePlayer.Rank = Mathf.Min( i, MaxRank );
                empirePlayer.EmpireId = generalId;
            }

            // handles empire change of hands
            if ( generalId == -1 )
            {
                // no more players in empire
                m_playersByEmpire.Remove( previousEmpireId );
            }
            else if ( generalId != previousEmpireId )
            {
                if ( m_playersByEmpire.ContainsKey( generalId ) )
                {
                    Debug.LogError( $"Empire with id of {generalId} was already present" );
                    CmdSetGameInfo( $"!Error setting empire ids!" );
                }
                m_playersByEmpire.Add( generalId, empirePlayerIds );
                m_playersByEmpire.Remove( previousEmpireId );
            }
        }

        // try add
        if ( !m_playersByEmpire.ContainsKey( empireId ) )
        {
            m_playersByEmpire.Add( empireId, new List<int>() );
        }

        // if we want to insert winners into certain ranks, we should do that here

        var empire = m_playersByEmpire[empireId];
        empire.Add( playerData.PlayerId );

        // need to do all players to keep empire size up to date
        for ( int i = 0; i < empire.Count; i++ )
        {
            var newEmpirePlayerId = empire[i];
            var newEmpirePlayer = m_allPlayersData[newEmpirePlayerId];
            newEmpirePlayer.Rank = Mathf.Min( i, MaxRank );
            newEmpirePlayer.EmpireId = empireId;
            newEmpirePlayer.EmpireSize = empire.Count;
        }

        UpdateAllPlayerData();
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


    [Command]
    private void CmdSetGameInfoForServerAndClient( string server, string client )
    {
        RpcSetGameInfoForServerAndClient( server, client );
    }

    [ClientRpc]
    private void RpcSetGameInfoForServerAndClient( string server, string client )
    {
        m_gameInfoTextChanged = true;
        if ( isServer )
        {
            m_gameInfo = server;
        }
        else
        {
            m_gameInfo = client;
        }
    }
}