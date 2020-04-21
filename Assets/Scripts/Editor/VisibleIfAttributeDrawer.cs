using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;


[CustomPropertyDrawer( typeof( VisibleIfAttribute ), false )]
public class VisibleIfAttributeDrawer : PropertyDrawer
{
    private const int MAX_ATTEMPTS = 10;
    readonly string[] m_debuggingMessages = new string[MAX_ATTEMPTS];

    private static readonly Dictionary<string, bool> s_propertiesVisibility = new Dictionary<string, bool>();

    public override void OnGUI ( Rect position, SerializedProperty property, GUIContent label )
    {
        var showIfAttribute = attribute as VisibleIfAttribute;

        int numChecked = 0;
        bool foundProperty = false;

        for ( int j = 1; j <= MAX_ATTEMPTS; j++ )
        {
            var conditionPath = ReplaceNthOccurrence( property.propertyPath, property.name, showIfAttribute.MemberName, j, true );
            if ( conditionPath == property.propertyPath )
            {
                break;
            }

            numChecked++;
            var member = property.serializedObject.FindProperty( conditionPath );
            if ( member == null )
            {
                m_debuggingMessages[j - 1] = string.Format( "Couldn't find property or member named '{0}' (propertyPath={1})", showIfAttribute.MemberName, conditionPath );
            }
            else
            {
                foundProperty = true;
                var propertyValue = GetPropertyValue( member );
                var propertyType = propertyValue.GetType();

                if ( !showIfAttribute.InvertEquality )
                {
                    for ( var i = 0; i < showIfAttribute.MemberValues.Length; ++i )
                    {
                        var attributeValue = Convert.ChangeType( showIfAttribute.MemberValues[i], propertyType );
                        if ( Equals( propertyValue, attributeValue ) )
                        {
                            EditorGUI.PropertyField( position, property, label, true );
                            return;
                        }
                    }
                }
                else
                {
                    for ( var i = 0; i < showIfAttribute.MemberValues.Length; ++i )
                    {
                        var attributeValue = Convert.ChangeType( showIfAttribute.MemberValues[i], propertyType );
                        if ( Equals( propertyValue, attributeValue ) )
                        {
                            s_propertiesVisibility[property.propertyPath] = false;
                            return;
                        }
                    }
                }
            }
        }

        if ( !foundProperty )
        {
            string totalDebugginString = "VisibleIf Attribute Could Not Resolve Predicate :\n";
            for ( var index = 0; index < m_debuggingMessages.Length; index++ )
            {
                string debuggingString = m_debuggingMessages[index];
                if ( !string.IsNullOrEmpty( debuggingString ) && index < numChecked )
                {
                    totalDebugginString += debuggingString + "\n";
                }
            }

            Debug.LogError( totalDebugginString );
        }

        if ( showIfAttribute.InvertEquality )
        {
            EditorGUI.PropertyField( position, property, label, true );
        }
        else
        {
            s_propertiesVisibility[property.propertyPath] = false;
        }
    }

    public override float GetPropertyHeight( SerializedProperty property, GUIContent label )
    {
        bool visible;
        if ( !s_propertiesVisibility.TryGetValue( property.propertyPath, out visible ) )
        {
            visible = false;
        }
        else
        {
            s_propertiesVisibility[property.propertyPath] = true;
        }

        return visible ? EditorGUI.GetPropertyHeight( property, label, true ) : 0.0f;
    }


    private static string ReplaceNthOccurrence( string obj, string find, string replace, int nthOccurrence, bool removeCharactersAfterMatch = false )
    {
        if ( nthOccurrence > 0 )
        {
            MatchCollection matchCollection = Regex.Matches( obj, Regex.Escape( find ) );
            if ( matchCollection.Count >= nthOccurrence )
            {
                Match match = matchCollection[nthOccurrence - 1];
                if ( removeCharactersAfterMatch )
                {
                    return obj.Remove( match.Index, obj.Length - match.Index ).Insert( match.Index, replace );
                }

                return obj.Remove( match.Index, match.Length ).Insert( match.Index, replace );
            }
        }
        return obj;
    }

    // From https://github.com/lordofduct/spacepuppy-unity-framework
    private static object GetPropertyValue( SerializedProperty prop )
    {
        if ( prop == null )
        {
            throw new ArgumentNullException( "prop" );
        }

        switch ( prop.propertyType )
        {
            case SerializedPropertyType.Integer:
                return prop.intValue;
            case SerializedPropertyType.Boolean:
                return prop.boolValue;
            case SerializedPropertyType.Float:
                return prop.floatValue;
            case SerializedPropertyType.String:
                return prop.stringValue;
            case SerializedPropertyType.Color:
                return prop.colorValue;
            case SerializedPropertyType.ObjectReference:
                return prop.objectReferenceValue;
            case SerializedPropertyType.LayerMask:
                return (LayerMask)prop.intValue;
            case SerializedPropertyType.Enum:
                return prop.enumValueIndex;
            case SerializedPropertyType.Vector2:
                return prop.vector2Value;
            case SerializedPropertyType.Vector3:
                return prop.vector3Value;
            case SerializedPropertyType.Vector4:
                return prop.vector4Value;
            case SerializedPropertyType.Rect:
                return prop.rectValue;
            case SerializedPropertyType.ArraySize:
                return prop.arraySize;
            case SerializedPropertyType.Character:
                return (char)prop.intValue;
            case SerializedPropertyType.AnimationCurve:
                return prop.animationCurveValue;
            case SerializedPropertyType.Bounds:
                return prop.boundsValue;
            case SerializedPropertyType.Gradient:
                throw new InvalidOperationException( "Can not handle Gradient types." );
            default:
                throw new InvalidOperationException( "Can not handle property type: " + prop.propertyType );
        }
    }
}