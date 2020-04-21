using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class DesignChoiceUI : MonoBehaviour
{
    [Serializable]
    private class Choice
    {
        public Button ChangeChoice;
        public Text ShowChoice;
    }

    [SerializeField] private Choice[] m_designChoiceUi;

    [SerializeField] private FieldInfo[] m_fields;

    private DesignChoices.GameRules m_gameRules;
    private Action<string> m_valueChanged;

    public void Initialise( DesignChoices.GameRules gameRules, Action<string> valueChangedCallback )
    {
        m_gameRules = gameRules;
        m_valueChanged = valueChangedCallback;

        //PropertyInfo[] myPropertyInfo;
        //myPropertyInfo = Type.GetType( "DesignChoices.GameRules" ).GetProperties();

        //var property = myPropertyInfo[0];
        //var reflected = property.ReflectedType;
        //var fields = reflected.GetFields();

        //foreach ( FieldInfo field in fields )
        //{
        //    Debug.Log( $"Fields: {field.Name}, {field.GetValue( gameRules )}" );
        //}

        for ( var index = 0; index < m_designChoiceUi.Length; index++ )
        {
            Choice choice = m_designChoiceUi[index];
            choice.ChangeChoice.onClick.RemoveAllListeners();
            if ( index == 0 )
            {
                choice.ChangeChoice.onClick.AddListener( () =>
                {
                    m_gameRules.UseMaze = !m_gameRules.UseMaze;
                    choice.ShowChoice.text = $"Use maze ? {m_gameRules.UseMaze}";
                    ValueChanged();
                } );

                choice.ShowChoice.text = $"Use maze ? {m_gameRules.UseMaze}";
            }
            else
            {
                choice.ChangeChoice.gameObject.SetActive( false );
                choice.ShowChoice.gameObject.SetActive( false );
            }
        }
    }

    private void ValueChanged()
    {
        m_valueChanged( m_gameRules.Serialize() );
    }
}