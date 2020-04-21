using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

[ExecuteAlways]
public class KeepOnGrid : MonoBehaviour
{
    [SerializeField] private Transform m_transform;
    [SerializeField] private float m_granularity = 1f;

    [Conditional( "UNITY_EDITOR" )]
    private void OnValidate()
    {
        if ( !m_transform )
        {
            m_transform = GetComponent<Transform>();
        }
    }

    public void Update()
    {
        if ( m_granularity.Equals( 0f ) )
        {
            Debug.Log("Granularity is zero, cannot divide by zero!");
            return;
        }

        var pos = m_transform.position;
        pos.x = RoundToFraction( pos.x, 1f / m_granularity );
        pos.y = RoundToFraction( pos.y, 1f / m_granularity );
        pos.z = RoundToFraction( pos.z, 1f / m_granularity );

        m_transform.position = pos;
    }

    private static float RoundToFraction( float original, float denominator )
    {
        var roundToFraction = Mathf.Round( original * denominator ) / denominator;
        return roundToFraction;
    }
}