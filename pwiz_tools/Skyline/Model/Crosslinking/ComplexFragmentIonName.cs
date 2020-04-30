using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIonName : Immutable
    {
        public static readonly ComplexFragmentIonName ORPHAN = new ComplexFragmentIonName()
        {
            IonType = IonType.custom,
        };

        public static readonly ComplexFragmentIonName PRECURSOR
            = new ComplexFragmentIonName(IonType.precursor, 0);

        public ComplexFragmentIonName(IonType ionType, int ordinal) : this()
        {
            IonType = ionType;
            if (IonType != IonType.precursor)
            {
                Ordinal = ordinal;
            }
        }

        private ComplexFragmentIonName()
        {
            Losses = ImmutableList<Tuple<ModificationSite, string>>.EMPTY;
            Children = ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>>.EMPTY;
        }

        public IonType IonType { get; private set; }
        public int Ordinal { get; private set; }
        public ImmutableList<Tuple<ModificationSite, string>> Losses { get; private set; }
        public ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>> Children { get; private set; }
        public bool IsOrphan
        {
            get { return IonType == IonType.custom; }
        }

        private static ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>> ToChildList(
            IEnumerable<Tuple<ModificationSite, ComplexFragmentIonName>> children)
        {
            return ImmutableList.ValueOf(children.OrderBy(tuple => tuple.Item1));
        }

        public ComplexFragmentIonName AddChild(ModificationSite modificationSite, ComplexFragmentIonName child)
        {
            if (IsOrphan)
            {
                if (Children.Count > 0)
                {
                    throw new InvalidOperationException();
                }
            }

            return ChangeProp(ImClone(this),
                im => { im.Children = ToChildList(Children.Append(Tuple.Create(modificationSite, child))); });
        }

        public ComplexFragmentIonName AddLoss(ModificationSite modificationSite, string loss)
        {
            if (IsOrphan)
            {
                throw new InvalidOperationException();
            }

            return ChangeProp(ImClone(this),
                im =>
                {
                    im.Losses = ImmutableList.ValueOf(im.Losses.Append(Tuple.Create(modificationSite, loss))
                        .OrderBy(tuple => tuple));
                });
        }

        protected bool Equals(ComplexFragmentIonName other)
        {
            return IonType == other.IonType && Ordinal == other.Ordinal && Losses.Equals(other.Losses) &&
                   Children.Equals(other.Children) && IsOrphan == other.IsOrphan;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ComplexFragmentIonName) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) IonType;
                hashCode = (hashCode * 397) ^ Ordinal;
                hashCode = (hashCode * 397) ^ Losses.GetHashCode();
                hashCode = (hashCode * 397) ^ Children.GetHashCode();
                hashCode = (hashCode * 397) ^ IsOrphan.GetHashCode();
                return hashCode;
            }
        }

        public ComplexFragmentIonName DisqualifyChildren()
        {
            return ChangeProp(ImClone(this),
                im =>
                {
                    im.Children = ImmutableList.ValueOf(im.Children.Select(child =>
                        new Tuple<ModificationSite, ComplexFragmentIonName>(null, child.Item2)));
                });
        }

        public override string ToString()
        {
            return ToString(CultureInfo.CurrentCulture, false);
        }

        public string ToPersistedString()
        {
            return ToString(CultureInfo.InvariantCulture, true);
        }

        private string ToString(CultureInfo culture, bool quoteNames)
        {
            if (IsOrphan && Children.Count == 0)
            {
                return @"-";
            }

            StringBuilder stringBuilder = new StringBuilder();
            if (!IsOrphan)
            {
                stringBuilder.Append(IonType);
                if (IonType != IonType.precursor)
                {
                    stringBuilder.Append(Ordinal);
                }
            }

            foreach (var loss in Losses)
            {
                stringBuilder.Append($@"({loss.Item1}[{loss.Item2}])");
            }

            if (Children.Count == 1 && Children[0].Item1 == null)
            {
                stringBuilder.Append(@"-");
                stringBuilder.Append(Children[0].Item2);
            }
            else if (Children.Count != 0)
            {
                stringBuilder.Append(@"-");
                if (Children.Count != 1)
                {
                    stringBuilder.Append(@"[");
                }

                stringBuilder.Append(string.Join(@",", Children.Select(ChildToString)));
                if (Children.Count != 1)
                {
                    stringBuilder.Append(@"]");
                }
            }

            return stringBuilder.ToString();
        }

        private string ChildToString(Tuple<ModificationSite, ComplexFragmentIonName> child)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(@"{");
            if (child.Item1 != null)
            {
                stringBuilder.Append(child.Item1);
                stringBuilder.Append(@":");
            }

            stringBuilder.Append(child.Item2);
            stringBuilder.Append(@"}");
            return stringBuilder.ToString();
        }

        public ComplexFragmentIon Resolve(TransitionGroup transitionGroup, ExplicitMods explicitMods)
        {
            ComplexFragmentIon fragmentIon;
            if (IsOrphan)
            {
                fragmentIon = ComplexFragmentIon.NewOrphanFragmentIon(transitionGroup, explicitMods, Adduct.SINGLY_PROTONATED);
            }
            else
            {
                int offset;
                if (IonType == IonType.precursor)
                {
                    offset = transitionGroup.Peptide.Length - 1;
                }
                else
                {
                    offset = Transition.OrdinalToOffset(IonType, Ordinal, transitionGroup.Peptide.Length);
                }

                var transition = new Transition(transitionGroup, IonType, offset, 0, Adduct.SINGLY_PROTONATED);
                fragmentIon = new ComplexFragmentIon(transition, null);
            }

            var crosslinks = explicitMods.Crosslinks;
            // TODO: losses
            foreach (var child in Children)
            {
                LinkedPeptide linkedPeptide;
                if (child.Item1 == null)
                {
                    if (crosslinks.Count != 1)
                    {
                        throw new ArgumentException(@"Must have only one crosslink modification");
                    }

                    linkedPeptide = crosslinks.Values.First();
                }
                else
                {
                    if (!crosslinks.TryGetValue(child.Item1, out linkedPeptide))
                    {
                        throw new ArgumentException(string.Format(@"No such crosslink {0}", child.Item1));
                    }
                }

                var childTransitionGroup = new TransitionGroup(linkedPeptide.Peptide,
                    transitionGroup.PrecursorAdduct, transitionGroup.LabelType);
                var resolvedChild = child.Item2.Resolve(childTransitionGroup, linkedPeptide.ExplicitMods);
                fragmentIon = fragmentIon.AddChild(child.Item1, resolvedChild);
            }

            return fragmentIon;
        }

#if false
        public static ComplexFragmentIonName Parse(string source)
        {
            using (var tokens = Tokenize(source.GetEnumerator()).GetEnumerator())
            {
                return Parse(tokens);
            }
        }
        private static ComplexFragmentIonName Parse(IEnumerator<string> tokens)
        {
            var current = ORPHAN;
            if (tokens.Current != @"{")
            {
                if (char.IsDigit(tokens.Current[tokens.Current.Length - 1]))
                {
                    current = new ComplexFragmentIonName(TypeSafeEnum.Parse<IonType>(tokens.Current.Substring(0, 1)),
                        int.Parse(tokens.Current.Substring(1)));
                }
                else if (tokens.Current == IonType.precursor.ToString())
                {
                    current = PRECURSOR;
                }
            }

            while (tokens.MoveNext())
            {
                if (tokens.Current == @"{")
                {
                    if (!tokens.MoveNext())
                    {
                        throw new FormatException();
                    }

                    ModificationSite currentSite = null;
                    if (char.IsDigit(tokens.Current[0]))
                    {
                        int aaIndex = int.Parse(tokens.Current);
                        if (!tokens.MoveNext())
                        {
                            throw new FormatException();
                        }

                        if (tokens.Current == @":")
                        {
                            if (!tokens.MoveNext())
                            {
                                throw new FormatException();
                            }
                            currentSite = new ModificationSite(aaIndex, UnquoteString(tokens.Current));
                            if (!tokens.MoveNext())
                            {
                                throw new FormatException();
                            }
                        }
                        else
                        {
                            currentSite = new ModificationSite(aaIndex, null);
                        }
                    }

                    var child = Parse(tokens);
                    current = current.AddChild()
                }
            }
            var stack = new Stack<Tuple<ModificationSite, ComplexFragmentIonName>>();
            ComplexFragmentIonName current = ComplexFragmentIonName.ORPHAN;
            ModificationSite currentSite = null;
            using (var tokens = Tokenize(input.GetEnumerator()).GetEnumerator())
            {
                string nextToken = null;
                while (true)
                {
                    if (nextToken == null)
                    {
                        if (!tokens.MoveNext())
                        {
                            break;
                        }

                        nextToken = tokens.Current;
                    }

                    if (nextToken == null || nextToken.Length != 0)
                    {
                        throw new InvalidOperationException();
                    }
                    if (char.IsDigit(nextToken[0]))
                    {
                        if (currentSite != null)
                        {
                            throw new FormatException();
                        }

                        currentSite = new ModificationSite(int.Parse(tokens.Current), null);
                        if (!tokens.MoveNext() || tokens.Current != @":")
                        {
                            throw new FormatException();
                        }

                        if (tokens.Current == @":")
                        {
                            if (!tokens.MoveNext())
                            {
                                throw new FormatException();
                            }
                            currentSite = new ModificationSite(currentSite.AaIndex, tokens.Current);
                            if (!tokens.MoveNext())
                            {
                                throw new FormatException();
                            }
                        }
                        nextToken = tokens.Current;
                    }
                    if (nextToken == "{")
                    {
                        stack.Push(Tuple.Create(currentSite, current));
                        currentSite = null;
                        current = ORPHAN;
                        continue;
                    }

                    if (!current.IsOrphan || current.Children.Count != 0)
                    {
                        throw new FormatException();
                    }

                    current = ParseSimpeIonType(nextToken);
                }
            }
        }

        static private ComplexFragmentIonName ParseSimpeIonType(string token)
        {
            if (token == IonType.precursor.ToString())
            {
                return new ComplexFragmentIonName(IonType.precursor, 0);
            }

            int ordinal = int.Parse(token.Substring(1));
            return new ComplexFragmentIonName(TypeSafeEnum.Parse<IonType>(token), ordinal);
        }

        [SuppressMessage("ReSharper", "LocalizableElement")]
        private static string UnquoteString(string token)
        {
            if (token.Length > 2 && token[0] == token[token.Length - 1])
            {
                return token.Substring(1, token.Length - 2).Replace("\"\"", "\"");
            }

            return token;
        }

        private static IEnumerable<string> Tokenize(IEnumerator<char> input)
        {
            StringBuilder currentToken = new StringBuilder();
            bool inQuote = false;
            while (input.MoveNext())
            {
                char ch = input.Current;
                if (inQuote)
                {
                    currentToken.Append(ch);
                    if (ch == '"')
                    {
                        inQuote = false;
                    }
                    continue;
                }

                if (ch == '"')
                {
                    if (currentToken.Length > 0)
                    {
                        if (currentToken[0] != '"')
                        {
                            yield return currentToken.ToString();
                            currentToken.Clear();
                        }
                    }

                    currentToken.Append(ch);
                    inQuote = true;
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    if (currentToken.Length > 0)
                    {
                        if (!char.IsLetterOrDigit(currentToken[0]))
                        {
                            yield return currentToken.ToString();
                            currentToken.Clear();
                        }
                    }

                    currentToken.Append(ch);
                    continue;
                }

                if (currentToken.Length > 0)
                {
                    yield return currentToken.ToString();
                    currentToken.Clear();
                }

                if (!char.IsWhiteSpace(ch))
                {
                    yield return ch.ToString();
                }
            }

            if (inQuote)
            {
                throw new FormatException();
            }

            if (currentToken.Length > 0)
            {
                yield return currentToken.ToString();
            }
        }
#endif

        public IEnumerable<SkylineDocumentProto.Types.LinkedIon> GetLinkedIonProtos()
        {
            foreach (var child in Children)
            {
                var proto = new SkylineDocumentProto.Types.LinkedIon()
                {
                    ModificationIndex = child.Item1.IndexAa,
                    ModificationName = child.Item1.ModName
                };

                if (child.Item2.IsOrphan)
                {
                    proto.Orphan = true;
                }
                else
                {
                    proto.IonType = DataValues.ToIonType(child.Item2.IonType);
                    proto.Ordinal = child.Item2.Ordinal;
                }
                proto.Children.AddRange(child.Item2.GetLinkedIonProtos());
                yield return proto;
            }
        }

        public static ComplexFragmentIonName FromLinkedIonProto(SkylineDocumentProto.Types.LinkedIon linkedIon)
        {
            ComplexFragmentIonName child;
            if (linkedIon.Orphan)
            {
                child = ORPHAN;
            }
            else
            {
                child = new ComplexFragmentIonName(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal);
            }

            child = child.AddLinkedIonProtos(linkedIon.Children);
            return child;
        }

        public ComplexFragmentIonName AddLinkedIonProtos(IEnumerable<SkylineDocumentProto.Types.LinkedIon> linkedIons)
        {
            var result = this;
            foreach (var linkedIon in linkedIons)
            {
                ComplexFragmentIonName child;
                if (linkedIon.Orphan)
                {
                    child = ORPHAN;
                }
                else
                {
                    child = new ComplexFragmentIonName(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal);
                }

                child = child.AddLinkedIonProtos(linkedIon.Children);
                result = result.AddChild(new ModificationSite(linkedIon.ModificationIndex, linkedIon.ModificationName), child);
            }

            return result;
        }
    }
}
