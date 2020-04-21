using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class DesignChoiceUI : MonoBehaviour
{
    [Serializable]
    private class Choice
    {
        public Button m_changeChoice;
        public Text m_showChoice;
    }

    [SerializeField] private DesignChoices m_designChoices;

    [SerializeField] private Choice[] m_designChoiceUi;

    private void Start()
    {
        PropertyInfo[] myPropertyInfo;
        myPropertyInfo = Type.GetType( "DesignChoices" ).GetProperties();
        foreach ( PropertyInfo propertyInfo in myPropertyInfo )
        {
            Debug.Log( $"Property: {propertyInfo.Name}, {propertyInfo.GetValue( m_designChoices )}" );
        }
    }
}