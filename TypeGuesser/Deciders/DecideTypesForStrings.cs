﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TB.ComponentModel;

namespace TypeGuesser.Deciders
{
    public abstract class DecideTypesForStrings<T> :IDecideTypesForStrings
    {
        public GuessSettings Settings { get; set; }

        public virtual CultureInfo Culture { get; protected set; }
        public TypeCompatibilityGroup CompatibilityGroup { get; private set; }
        public HashSet<Type> TypesSupported { get; private set; }

        /// <summary>
        /// Matches any number which looks like a proper decimal but has leading zeroes e.g. 012837 including.  Also matches if there is a
        /// decimal point (optionally followed by other digits).  It must match at least 2 digits at the start e.g. 01.01 would be matched
        /// but 0.01 wouldn't be matched (that's a legit float).  This is used to preserve leading zeroes in input (desired because it could
        /// be a serial number or otherwise important leading 0).  In this case the <see cref="Guesser"/> will use varchar(x) to represent the 
        /// column instead of decimal(x,y).
        /// 
        /// <para>Also allows for starting with a negative sign e.g. -01.01 would be matched as a string</para>
        /// <para>Also allows for leading / trailing whitespace</para>
        /// 
        /// </summary>
        readonly Regex zeroPrefixedNumber = new Regex(@"^\s*-?0+[1-9]+\.?[0-9]*\s*$");

        protected DecideTypesForStrings(CultureInfo culture, TypeCompatibilityGroup compatibilityGroup,params Type[] typesSupported)
        {
            Culture = culture;

            Settings = new GuessSettingsFactory().Create();

            CompatibilityGroup = compatibilityGroup;
            
            if(typesSupported.Length == 0)
                throw new ArgumentException(SR.DecideTypesForStrings_DecideTypesForStrings_DecideTypesForStrings_abstract_base_was_not_passed_any_typesSupported_by_implementing_derived_class);
            
            TypesSupported = new HashSet<Type>(typesSupported);
        }

        public bool IsAcceptableAsType(string candidateString,IDataTypeSize size)
        {
            //we must preserve leading zeroes if its not actually 0 -- if they have 010101 then we have to use string but if they have just 0 we can use decimal
            if (zeroPrefixedNumber.IsMatch(candidateString))
                return false;

            return IsAcceptableAsTypeImpl(candidateString, size);
        }

        public object Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            try
            {
                return ParseImpl(value);
            }catch(Exception ex)
            {
                throw new FormatException(string.Format(SR.DecideTypesForStrings_Parse_Could_not_parse_string_value___0___with_Decider_Type__1_, value, GetType().Name),ex);
            }            
        }

        public IDecideTypesForStrings Clone()
        {
            IDecideTypesForStrings clone = CloneImpl(Culture);
            clone.Settings = Settings.Clone();
            return clone;
        }

        /// <summary>
        /// Create a new instance of Type {T}
        /// </summary>
        /// <param name="culture"></param>
        /// <returns></returns>
        protected abstract IDecideTypesForStrings CloneImpl(CultureInfo culture);

        protected virtual object ParseImpl(string value)
        {
            return value.To<T>(Culture);
        }

        protected virtual bool IsAcceptableAsTypeImpl(string candidateString,IDataTypeSize size)
        {
            return candidateString.IsConvertibleTo<T>(Culture);
        }
    }
}