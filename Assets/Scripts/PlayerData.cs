using System;
using UnityEngine;

[Serializable]
public class PlayerData
{
    public int PlayerId = -1;
    public int EmpireId = -1;
    public int Rank;
    public int AllowedMovements;
    public int EmpireSize = 1;

    public void SetData( PlayerData other )
    {
        if ( PlayerId != -1 )
        {
            Debug.Assert( PlayerId == other.PlayerId, $"PlayerData objects have different player IDs (prev={PlayerId}, new={other.PlayerId}), this has already been set" );
        }

        PlayerId = other.PlayerId;
        EmpireId = other.EmpireId;
        Rank = other.Rank;
        AllowedMovements = other.AllowedMovements;

        Debug.Log( $"Setting player data for player id {PlayerId}: AllowedMoves={AllowedMovements}, EmpireId={EmpireId}, Rank={Rank}" );
    }

    public string Serialize()
    {
        return JsonUtility.ToJson( this );
    }

    public PlayerData Deserialize( string serialized )
    {
        return JsonUtility.FromJson< PlayerData >( serialized );
    }
}