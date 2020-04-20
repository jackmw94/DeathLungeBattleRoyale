using UnityEngine;
using UnityEngine.Networking;

public partial class PlayerController
{
    [Command]
    private void CmdKickPlayer( int kickPlayerId )
    {
        var gameController = GameController.Instance;
        gameController.QueueBattle( this, kickPlayerId );
    }

    [Command]
    private void CmdRespondToFightRequest( FightUI.FightChoice fightResponse )
    {
        var gameController = GameController.Instance;
        gameController.SubmitFightChoice( this, fightResponse );
    }

    [Command]
    public void CmdSetEmpire( int empireId )
    {
        RpcSetEmpire( empireId );

    }

    [ClientRpc]
    private void RpcSetEmpire( int empireId )
    {
        m_empireId = empireId;
        m_playerColourIndicator.color = m_colours.GetColour( m_empireId );
    }

    [Command]
    private void CmdRespondToMoveRequest( int moves )
    {
        Debug.Log( $"Player {PlayerId} is responding to move request with {moves}" );
        var gameController = GameController.Instance;
        gameController.SubmitMoveCount( this, moves );
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
        m_transform.position = GetStartPosition( m_playerId );
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
    private void CmdSetPlayerIndex()
    {
        var index = GameController.Instance.RegisterPlayer( this );
        m_playerId = index;
        m_empireId = index;
    }

    [Command]
    public void CmdSetAllowedMoves( int moves )
    {
        moves = Mathf.Clamp( moves, 0, MaxMoves );
        m_allowedMovements = moves;
    }

    [Command]
    public void CmdSetPlayerRank( int rank )
    {
        m_rank = rank;
    }
}