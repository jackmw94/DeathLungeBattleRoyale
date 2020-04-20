using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class MoveIndicator : MonoBehaviour
{
    [SerializeField] private List<GameObject> m_indicatorObjects;
    [SerializeField] private float m_indicatorSize;
    [SerializeField] private float m_distanceFromCentre;
    [SerializeField] private bool m_updatePositions;

    [Conditional("UNITY_EDITOR")]
    private void OnValidate ()
    {
        if ( m_indicatorObjects.Count == 0 )
        {
            m_indicatorObjects = new List< GameObject >();
            for ( int i = 0; i < transform.childCount; i++ )
            {
                m_indicatorObjects.Add(transform.GetChild( i ).gameObject);
            }
        }

        if ( m_updatePositions )
        {
            m_updatePositions = false;
            int objectCount = m_indicatorObjects.Count;
            for( int i = 0; i < transform.childCount; i++ )
            {
                float angle = i * ( 360f / objectCount );
                Vector2 pos = Vector2.up * m_distanceFromCentre;
                pos = Rotate( pos, angle );
                m_indicatorObjects[i].transform.localScale = Vector3.one * m_indicatorSize;
                m_indicatorObjects[i].transform.localPosition = new Vector3( pos.x, 0f, pos.y );
            }
        }
    }

    public static Vector2 Rotate( Vector2 v, float degree )
    {
        float rad = degree * Mathf.Deg2Rad;
        float c = Mathf.Cos( rad );
        float s = Mathf.Sin( rad );
        return new Vector2( c * v.x - s * v.y, s * v.x + c * v.y );
    }

    public void SetMoves ( int moves )
    {
        for ( int i = 0; i < m_indicatorObjects.Count; i++ )
        {
            m_indicatorObjects[i].SetActive( moves > i );
        }
    }
}
