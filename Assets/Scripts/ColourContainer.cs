using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ColourContainer", menuName = "Create colour container")]
public class ColourContainer : ScriptableObject
{
    [SerializeField, FormerlySerializedAs("m_colours")] private Color[] m_playerColours;

    [SerializeField] private Color[] m_empireColours;

    private int m_offset = 0;

    public void ChangeOffset ()
    {
        m_offset = Random.Range( 0, 8 );
    }

    public Color GetPlayerColour ( int index )
    {
        var colourIndex = (index + m_offset) % m_playerColours.Length;
        return m_playerColours[colourIndex];
    }

    public Color GetEmpireColour( int index )
    {
        var colourIndex = ( index + m_offset ) % m_playerColours.Length;
        return m_empireColours[colourIndex];
    }
}