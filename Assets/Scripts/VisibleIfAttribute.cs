using System;
using UnityEngine;


[AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true )]
public class VisibleIfAttribute : PropertyAttribute
{
    public readonly string MemberName;

    public readonly object[] MemberValues;

    public readonly bool InvertEquality;
    
    public VisibleIfAttribute( string memberName, params object[] values )
    {
        MemberName = memberName;
        MemberValues = values;
    }
    
    public VisibleIfAttribute( bool invertEquality, string memberName, params object[] values )
    {
        MemberName = memberName;
        MemberValues = values;
        InvertEquality = invertEquality;
    }
}