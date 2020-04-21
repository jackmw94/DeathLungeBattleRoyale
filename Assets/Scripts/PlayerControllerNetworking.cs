using UnityEngine;
using UnityEngine.Networking;

public partial class PlayerController
{
    [Command]
    private void CmdMoveTaken( bool hasKicked )
    {
        var gameController = GameController.Instance;
        gameController.PlayerMoveTaken( m_playerData.PlayerId, hasKicked );
    }

    [Command]
    private void CmdKickPlayer( int kickPlayerId )
    {
        var gameController = GameController.Instance;
        gameController.QueueBattle( m_playerData.PlayerId, kickPlayerId );
    }

    [Command]
    private void CmdRespondToFightRequest( FightUI.FightChoice fightResponse )
    {
        var gameController = GameController.Instance;
        gameController.SubmitFightChoice( m_playerData.PlayerId, fightResponse );
    }

    [Command]
    private void CmdRespondToMoveRequest( int moves )
    {
        Debug.Log( $"Player {PlayerId} is responding to move request with {moves}" );
        var gameController = GameController.Instance;
        gameController.SubmitMoveCount( m_playerData.PlayerId, moves );
    }

    [ClientRpc]
    public void RpcRequestFightResponse()
    {
        if ( m_networkIdentity.isLocalPlayer )
        {
            m_fightUi.ShowRequest( RespondToFightRequest );
        }
    }

    [ClientRpc]
    public void RpcResetPosition()
    {
        m_transform.position = GetStartPosition( PlayerId );
    }

    [ClientRpc]
    public void RpcRequestMoves()
    {
        if ( m_networkIdentity.isLocalPlayer )
        {
            m_moveRequestUi.ShowRequest( RespondToMoveRequest );
        }
    }

    [Command]
    private void CmdRegisterPlayer()
    {
        GameController.Instance.RegisterPlayer( this );
    }

    [ClientRpc]
    public void RpcUpdatePlayerData( string serializedPlayerData )
    {
        var playerData = PlayerData.Deserialize( serializedPlayerData );
        m_playerData.SetData( playerData );

        m_playerColourIndicator.material.color = m_colours.GetPlayerColour( playerData.PlayerId );
        m_empireColourIndicator.material.color = m_colours.GetEmpireColour( playerData.EmpireId );

        for ( int i = 0; i < m_prestige.Length; i++ )
        {
            m_prestige[i].SetActive( i == playerData.Rank );
        }

        if ( m_networkIdentity.isLocalPlayer )
        {
            bool inputEnabled = AllowedMoves > 0;
            Debug.Log( $"Setting player with id {playerData.PlayerId}'s input to be {inputEnabled}" );
            m_playerInput.enabled = inputEnabled;
        }

        m_movementDisabledUntilDataUpdated = false;

        m_moveIndicator.SetMoves( AllowedMoves );

        if ( !m_hasSetData )
        {
            m_hasSetData = true;
            if ( m_networkIdentity.isLocalPlayer )
            {
                m_transform.position = GetStartPosition( PlayerId );
            }
            gameObject.name = $"Player {PlayerId}";
        }

        // handle rank decor
    }
}