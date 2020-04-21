using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

[CreateAssetMenu( menuName = "Create design choice config", fileName = "DesignChoices" )]
public class DesignChoices : ScriptableObject
{
    public enum FightTargetRule
    {
        FightAnyone,
        FightEnemiesOnly,
        InternalFightOneAboveRank,
        InternalFightAnyAboveRank,
    }

    public enum OppositionFightResolutionRule
    {
        LosingPlayerJoinsWinner,
        LosingEmpireJoinsWinner,
        LosingPlayerAndAllLowerRanksJoinsWinner
    }

    public enum InternalFightResolutionRule
    {
        SwapsRanks,
        TakesRankAndMoveDown,
    }

    public enum RankStructureSetting
    {
        TwoRanks,
        ThreeRanks,
        ContinuousRanks
    }

    [Serializable]
    public class GameRules
    {
        [Space( 10 )] public bool UseMaze = false;
        [Space( 10 )] public RankStructureSetting RankStructure = RankStructureSetting.ThreeRanks;
        [Space( 10 )] public FightTargetRule FightingRule;
        
        public OppositionFightResolutionRule PrivateBeatsOpposingGeneral;
        [VisibleIf( true, nameof ( RankStructure ), RankStructureSetting.TwoRanks )] public OppositionFightResolutionRule PrivateBeatsOpposingSergeant;
        public OppositionFightResolutionRule PrivateBeatsOpposingPrivate;

        [Space( 10 )]
        [VisibleIf( true, nameof ( RankStructure ), RankStructureSetting.TwoRanks )] public OppositionFightResolutionRule SergeantBeatsOpposingGeneral;
        [VisibleIf( true, nameof ( RankStructure ), RankStructureSetting.TwoRanks )] public OppositionFightResolutionRule SergeantBeatsOpposingSergeant;
        [VisibleIf( true, nameof ( RankStructure ), RankStructureSetting.TwoRanks )] public OppositionFightResolutionRule SergeantBeatsOpposingPrivate;

        [Space( 10 )]
        public OppositionFightResolutionRule GeneralBeatsOpposingGeneral;
        [VisibleIf( true, nameof ( RankStructure ), RankStructureSetting.TwoRanks )] public OppositionFightResolutionRule GeneralBeatsOpposingSergeant;
        public OppositionFightResolutionRule GeneralBeatsOpposingPrivate;

        [VisibleIf( true, nameof( FightingRule ), FightTargetRule.FightEnemiesOnly )] public InternalFightResolutionRule PrivateBeatsOwnGeneral;
        [VisibleIf( true, nameof( FightingRule ), FightTargetRule.FightEnemiesOnly ),
         VisibleIf( true, nameof( RankStructure ), RankStructureSetting.TwoRanks )] public InternalFightResolutionRule PrivateBeatsOwnSergeant;

        [Space( 10 )]

        [VisibleIf( true, nameof( FightingRule ), FightTargetRule.FightEnemiesOnly ), VisibleIf( true, nameof( RankStructure ), RankStructureSetting.TwoRanks )]
        public InternalFightResolutionRule SergeantBeatsOwnGeneral;

        public string Serialize ()
        {
            return JsonUtility.ToJson( this );
        }

        public GameRules Deserialize ( string serialized )
        {
            return JsonUtility.FromJson< GameRules >( serialized );
        }

        public void SetChoices ( GameRules other )
        {
            UseMaze = other.UseMaze;
            RankStructure = other.RankStructure;
            FightingRule = other.FightingRule;

            PrivateBeatsOpposingGeneral = other.PrivateBeatsOpposingGeneral;
            PrivateBeatsOpposingSergeant = other.PrivateBeatsOpposingSergeant;
            PrivateBeatsOpposingPrivate = other.PrivateBeatsOpposingPrivate;

            SergeantBeatsOpposingGeneral = other.SergeantBeatsOpposingGeneral;
            SergeantBeatsOpposingSergeant = other.SergeantBeatsOpposingSergeant;
            SergeantBeatsOpposingPrivate = other.SergeantBeatsOpposingPrivate;

            GeneralBeatsOpposingGeneral = other.GeneralBeatsOpposingGeneral;
            GeneralBeatsOpposingSergeant = other.GeneralBeatsOpposingSergeant;
            GeneralBeatsOpposingPrivate = other.GeneralBeatsOpposingPrivate;

            PrivateBeatsOwnGeneral = other.PrivateBeatsOwnGeneral;
            PrivateBeatsOwnSergeant = other.PrivateBeatsOwnSergeant;

            SergeantBeatsOwnGeneral = other.SergeantBeatsOwnGeneral;
        }
    }

    public GameRules GameDesignConfig;

#if UNITY_EDITOR
    public static DesignChoices GetInstanceFromAssets()
    {
        string[] settings = UnityEditor.AssetDatabase.FindAssets( $"t:DesignChoices" );
        if ( settings.Length == 0 )
        {
            Debug.LogError( $"No design choice config found" );
            return null;
        }
        Debug.Assert( settings.Length == 1, "More than one DesignChoices asset found" );
        var path = UnityEditor.AssetDatabase.GUIDToAssetPath( settings[0] );
        var designChoices = UnityEditor.AssetDatabase.LoadAssetAtPath<DesignChoices>( path );
        return designChoices;
    }
#endif

}