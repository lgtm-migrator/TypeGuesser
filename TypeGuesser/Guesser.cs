using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;

namespace TypeGuesser
{
    /// <summary>
    /// Calculates a <see cref="DatabaseTypeRequest"/> based on a collection of objects seen so far.  This allows you to take a DataTable column (which might be only string
    /// formatted) and identify an appropriate database type to hold the data.  For example if you see "2001-01-01" in the first row of column then the database
    /// type can be 'datetime' but if you subsequently see 'n\a' then it must become 'varchar(10)' (since 2001-01-01 is 10 characters long).
    /// 
    /// <para>Includes support for DateTime, Timespan, String (including calculating max length), Int, Decimal (including calculating scale/precision). </para>
    /// 
    /// <para><see cref="Guesser"/> will always use the most restrictive data type possible first and then fall back on weaker types as new values are seen that do not fit
    /// the guessed Type, ultimately falling back to varchar(x).</para>
    /// </summary>
    public class Guesser 
    {
        public GuessSettings Settings => _typeDeciders.Settings;

        /// <summary>
        /// Normally when measuring the lengths of strings something like "It�s" would be 4 but for Oracle it needs extra width.  If this is
        /// non zero then when <see cref="AdjustToCompensateForValue(object)"/> is a string then any non standard characters will have this number
        /// added to the length predicted.
        /// </summary>
        public int ExtraLengthPerNonAsciiCharacter { get; set; }

        /// <summary>
        /// The minimum amount of characters required to represent date values stored in the database when issuing ALTER statement to convert
        /// the column to allow strings.
        /// </summary>
        public const int MinimumLengthRequiredForDateStringRepresentation = 27;

        /// <summary>
        /// The currently computed data type (including string length / decimal scale/precisione etc) that can store all values seen
        /// by <see cref="AdjustToCompensateForValue"/> so far.
        /// </summary>
        public DatabaseTypeRequest Guess { get; set; }

        /// <summary>
        /// The culture to use for type deciders, determines what symbol decimal place is etc
        /// </summary>
        public CultureInfo Culture {
            set => _typeDeciders  = new TypeDeciderFactory(value);
        } 

        private TypeDeciderFactory _typeDeciders;
        

        public bool IsPrimedWithBonafideType = false;

        /// <summary>
        /// Previous data types we have seen and used to adjust our CurrentEstimate.  It is important to record these, because if we see
        /// an int and change our CurrentEstimate to int then we can't change our CurrentEstimate to datetime later on because that's not
        /// compatible with int. See test TestGuesser_IntToDateTime
        /// </summary>
        TypeCompatibilityGroup _validTypesSeen = TypeCompatibilityGroup.None;

        /// <summary>
        /// Creates a new DataType 
        /// </summary>
        public Guesser():this(new DatabaseTypeRequest(DatabaseTypeRequest.PreferenceOrder[0]))
        {
            
        }


        /// <summary>
        /// Creates a new <see cref="Guesser"/> primed with the size of the given <paramref name="request"/>.
        /// </summary>
        /// <param name="request"></param>
        public Guesser(DatabaseTypeRequest request)
        {
            Guess = request;
            _typeDeciders = new TypeDeciderFactory(CultureInfo.CurrentCulture);
            
            ThrowIfNotSupported(request.CSharpType);
        }
        
        public void AdjustToCompensateForValues(DataColumn column)
        {
            var dt = column.Table;
            foreach (DataRow row in dt.Rows)
                AdjustToCompensateForValue(row[column]);
        }
        public void AdjustToCompensateForValues(IEnumerable<object> collection)
        {
            foreach (var o in collection)
                AdjustToCompensateForValue(o);
        }

        public void AdjustToCompensateForValue(object o)
        {
            if(o == null)
                return;

            if(o == DBNull.Value)
                return;

            //if we have previously seen a hard typed value then we can't just change datatypes to something else!
            if (IsPrimedWithBonafideType)
                if (Guess.CSharpType != o.GetType())
                    throw new MixedTypingException(string.Format(
                        SR.Guesser_AdjustToCompensateForValue_GuesserPassedMixedTypeValues,
                        o, o.GetType(), Guess.CSharpType));

            var oToString = o.ToString();
            var oAsString = o as string;

            //we might need to fallback on a string later on, in this case we should always record the maximum length of input seen before even if it is acceptable as int, double, dates etc
            Guess.Width = Math.Max(Guess.Width??-1, GetStringLength(oToString));

            //if it's a string
            if (oAsString != null)
            {
                //ignore empty ones
                if (string.IsNullOrWhiteSpace(oAsString))
                    return;
                
                //if we have already fallen back to string then just stick with it (theres no going back up the ladder)
                if(Guess.CSharpType == typeof(string))
                    return;

                var result = _typeDeciders.Dictionary[Guess.CSharpType].IsAcceptableAsType(oAsString, Guess);
                
                //if the current estimate compatible
                if (result)
                {
                    _validTypesSeen = _typeDeciders.Dictionary[Guess.CSharpType].CompatibilityGroup;

                    if (Guess.CSharpType == typeof (DateTime))
                        Guess.Width = Math.Max(Guess.Width??-1, MinimumLengthRequiredForDateStringRepresentation);


                    return;
                }

                //if it isn't compatible, try the next Type
                ChangeEstimateToNext();

                //recurse because why not
                AdjustToCompensateForValue(oAsString);
            }
            else
            {
                //if we ever made a decision about a string inputs then we won't accept hard typed objects now
                if(_validTypesSeen != TypeCompatibilityGroup.None || Guess.CSharpType == typeof(string))
                    throw new MixedTypingException(string.Format(SR.Guesser_AdjustToCompensateForValue_GuesserPassedMixedTypeValues,o,o.GetType(),Guess));

                //if we have yet to see a proper type
                if (!IsPrimedWithBonafideType)
                {
                    Guess.CSharpType = o.GetType();//get its type
                    IsPrimedWithBonafideType = true;
                }

                //if we have a decider for this lets get it to tell us the decimal places (if any)
                if (_typeDeciders.Dictionary.ContainsKey(o.GetType()))
                    _typeDeciders.Dictionary[o.GetType()].IsAcceptableAsType(oToString, Guess);
            }
        }

        private int GetStringLength(string oToString)
        {
            var nonAscii = oToString.Count(IsNotAscii);

            if(nonAscii > 0)
                Guess.Unicode = true;

            if(ExtraLengthPerNonAsciiCharacter == 0)
                return oToString.Length;

            return oToString.Length + (nonAscii * ExtraLengthPerNonAsciiCharacter);
        }

        private bool IsNotAscii(char arg)
        {
            return (int)arg >= 127;
        }

        private void ChangeEstimateToNext()
        {
            int current = DatabaseTypeRequest.PreferenceOrder.IndexOf(Guess.CSharpType);
            
            //if we have never seen any good data just try the next one
            if(_validTypesSeen == TypeCompatibilityGroup.None )
                Guess.CSharpType = DatabaseTypeRequest.PreferenceOrder[current + 1];
            else
            {
                //we have seen some good data before, but we have seen something that doesn't fit with the CurrentEstimate so
                //we need to degrade the Estimate to a new Type that is compatible with all the Types previously seen
                
                var nextEstiamte = DatabaseTypeRequest.PreferenceOrder[current + 1];

                //if the next estimate is a string or we have previously accepted an exclusive decider (e.g. DateTime)
                if (nextEstiamte == typeof (string) || _validTypesSeen == TypeCompatibilityGroup.Exclusive)
                    Guess.CSharpType = typeof (string); //then just go with string
                else
                {
                    //if the next decider is in the same group as the previously used ones
                    if (_typeDeciders.Dictionary[nextEstiamte].CompatibilityGroup == _validTypesSeen)
                        Guess.CSharpType = nextEstiamte;
                    else
                        Guess.CSharpType = typeof (string); //the next Type decider is in an incompatible category so just go directly to string
                }
            }
        }
        

        /// <summary>
        /// Returns true if the <see cref="Guess"/>  is considered to be an improvement on the DataColumn provided. Use only when you actually want to
        /// consider changing the value.  For example if you have read a CSV file into a DataTable and all current columns string/object then you can call this method
        /// to determine whether the <see cref="Guesser"/> found a more appropriate Type or not.  
        /// 
        /// <para>Note that if you want to change the Type you need to clone the DataTable, see: https://stackoverflow.com/questions/9028029/how-to-change-datatype-of-a-datacolumn-in-a-datatable</para>
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        public bool ShouldDowngradeColumnTypeToMatchCurrentEstimate(DataColumn col)
        {
            if(col.DataType == typeof(object) || col.DataType == typeof(string))
            {
                int indexOfCurrentPreference = DatabaseTypeRequest.PreferenceOrder.IndexOf(Guess.CSharpType);
                int indexOfCurrentColumn = DatabaseTypeRequest.PreferenceOrder.IndexOf(typeof(string));
                
                //e.g. if current preference based on data is DateTime/integer and col is a string then we SHOULD downgrade
                return indexOfCurrentPreference < indexOfCurrentColumn;
            }

            //it's not a string or an object, user probably has a type in mind for his DataColumn, let's not change that
            return false;
        }

        private void ThrowIfNotSupported(Type currentEstimate)
        {
            if (currentEstimate == typeof(string))
                return;

            if (!_typeDeciders.IsSupported(Guess.CSharpType))
                throw new NotSupportedException(string.Format(SR.Guesser_ThrowIfNotSupported_No_Type_Decider_exists_for_Type__0_, Guess.CSharpType));
        }

        public object Parse(string val)
        {
            if (Guess.CSharpType == typeof(string))
                return val;

            ThrowIfNotSupported(Guess.CSharpType);

            return _typeDeciders.Dictionary[Guess.CSharpType].Parse(val);
        }
    }
}
