using System;
using UnityEngine;
using UnityEngine.UI;

public class MoveRequestUI : MonoBehaviour
{
    [SerializeField] private Transform m_uiRoot;
    [SerializeField] private Button[] m_buttons;

    private Action<int> m_callback = null;

    private void Start()
    {
        for ( int i = 0; i < m_buttons.Length; i++ )
        {
            var moves = i;
            m_buttons[i].onClick.AddListener( () =>
            {
                ButtonListener( moves + 1 );
            } );
        }
    }

    private void OnDestroy()
    {
        for ( int i = 0; i < m_buttons.Length; i++ )
        {
            m_buttons[i].onClick.RemoveAllListeners();
        }
    }
    
    public void ShowRequest( Action<int> callback )
    {
        Debug.Log( $"Showing move request UI" );
        m_callback = callback;
        m_uiRoot.gameObject.SetActive( true );
    }

    private void ButtonListener( int moveValue )
    {
        Debug.Log( $"Returning from move request ui with {moveValue}" );
        m_uiRoot.gameObject.SetActive( false );
        m_callback( moveValue );
    }
}