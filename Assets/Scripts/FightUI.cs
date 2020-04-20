using System;
using UnityEngine;
using UnityEngine.UI;

public class FightUI : MonoBehaviour
{
    public enum FightChoice
    {
        None, Rock, Paper, Scissors
    }
    [SerializeField] private Transform m_uiRoot;

    [SerializeField] private Button m_rockButton;
    [SerializeField] private Button m_paperButton;
    [SerializeField] private Button m_scissorsButton;

    private Action< FightChoice > m_callback = null;

    private void Start()
    {
        m_rockButton.onClick.AddListener( () => { RespondToRequest( FightChoice.Rock ); } );
        m_paperButton.onClick.AddListener( () => { RespondToRequest( FightChoice.Paper ); } );
        m_scissorsButton.onClick.AddListener( () => { RespondToRequest( FightChoice.Scissors ); } );
    }

    private void OnDestroy ()
    {
        m_rockButton.onClick.RemoveAllListeners();
        m_paperButton.onClick.RemoveAllListeners();
        m_scissorsButton.onClick.RemoveAllListeners();
    }

    public void ShowRequest( Action<FightChoice> callback )
    {
        m_callback = callback;
        m_uiRoot.gameObject.SetActive( true );
    }

    private void RespondToRequest( FightChoice fight )
    {
        m_uiRoot.gameObject.SetActive( false );
        m_callback( fight );
    }
}