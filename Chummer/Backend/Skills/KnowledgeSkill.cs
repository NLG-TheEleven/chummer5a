using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Chummer.Backend.Equipment;
using Chummer.Datastructures;

namespace Chummer.Skills
{
    public class KnowledgeSkill : Skill
    {
        private static readonly TranslatedField<string> _translator = new TranslatedField<string>();
        private static readonly Dictionary<string, string> CategoriesSkillMap = new Dictionary<string, string>();  //Categories to their attribtue
        private static readonly Dictionary<string, string> NameCategoryMap = new Dictionary<string, string>();  //names to their category

        public static List<ListItem> DefaultKnowledgeSkillCatagories { get; }
        public static List<ListItem> KnowledgeTypes { get; }  //Load the (possible translated) types of kno skills (Academic, Street...)

        static KnowledgeSkill()
        {
            XmlDocument objXmlDocument = XmlManager.Load("skills.xml");

            XmlNodeList objXmlCategoryList = objXmlDocument.SelectNodes("/chummer/categories/category[@type = \"knowledge\"]");

            KnowledgeTypes = (from XmlNode objXmlCategory in objXmlCategoryList
                let display = objXmlCategory.Attributes?["translate"]?.InnerText ?? objXmlCategory.InnerText
                orderby display
                select new ListItem(objXmlCategory.InnerText, display)).ToList();

            XmlNodeList objXmlSkillList = objXmlDocument.SelectNodes("/chummer/knowledgeskills/skill");
            DefaultKnowledgeSkillCatagories = new List<ListItem>();
            foreach (XmlNode objXmlSkill in objXmlSkillList)
            {
                string strSkillName = objXmlSkill["name"]?.InnerText;
                if (string.IsNullOrWhiteSpace(strSkillName))
                    continue;
                string display = objXmlSkill["translate"]?.InnerText ?? strSkillName;

                if (GlobalOptions.Language != "en-us")
                {
                    _translator.Add(strSkillName, display);
                }

                DefaultKnowledgeSkillCatagories.Add(new ListItem(strSkillName, display));

                string strCategory = objXmlSkill["category"]?.InnerText;
                if (!string.IsNullOrWhiteSpace(strCategory))
                {
                    NameCategoryMap[strSkillName] = strCategory;
                    CategoriesSkillMap[strCategory] = objXmlSkill["attribute"]?.InnerText;
                }
            }

            DefaultKnowledgeSkillCatagories = DefaultKnowledgeSkillCatagories.OrderBy(x => x.Name).ToList();
        }

        public static List<ListItem> KnowledgeSkillsWithCategory(params string[] categories)
        {
            HashSet<string> set = new HashSet<string>(categories);

            return DefaultKnowledgeSkillCatagories.Where(x => set.Contains(NameCategoryMap[x.Value])).ToList();
        } 

        public override bool AllowDelete
        {
            get { return true; } //TODO LM
        }

        private string _translated; //non english name, if present
        private List<ListItem> _knowledgeSkillCatagories;
        private string _type;
        public bool ForcedName { get; }

        public KnowledgeSkill(Character character) : base(character, (string)null)
        {
            AttributeObject = character.LOG;
            AttributeObject.PropertyChanged += OnLinkedAttributeChanged;
            SuggestedSpecializations = new List<ListItem>();
        }

        public KnowledgeSkill(Character character, string forcedName) : this(character)
        {
            WriteableName = forcedName;
            LoadDefaultType(Name);
            ForcedName = true;
        }

        public List<ListItem> KnowledgeSkillCatagories
        {
            get { return _knowledgeSkillCatagories ?? DefaultKnowledgeSkillCatagories; }
            set
            {
                _knowledgeSkillCatagories = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomSkillCatagories));
            }
        }

        public bool CustomSkillCatagories
        {
            get { return _knowledgeSkillCatagories != null; }
        }

        public string WriteableName
        {
            get { return _translator.Read(Name, ref _translated); }
            set
            {
                if (ForcedName)
                    return;
                string strOriginal = Name;
                _translator.Write(value, ref strOriginal, ref _translated);
                Name = strOriginal;
                LoadSuggestedSpecializations(Name);

                OnPropertyChanged();
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(Base));
            }
        }

        private void LoadSuggestedSpecializations(string name)
        {
            string strNameValue;
            if (NameCategoryMap.TryGetValue(name, out strNameValue))
            {
                SuggestedSpecializations.Clear();

                XmlNodeList list =
                    XmlManager.Load("skills.xml").SelectNodes($"chummer/knowledgeskills/skill[name = \"{name}\"]/specs/spec");
                foreach (XmlNode node in list)
                {
                    SuggestedSpecializations.Add(ListItem.AutoXml(node.InnerText, node));
                }

                SortListItem objSort = new SortListItem();
                SuggestedSpecializations.Sort(objSort.Compare);
                OnPropertyChanged(nameof(CGLSpecializations));
            }
        }

        public void LoadDefaultType(string name)
        {
            if (name == null)
                return;
            //TODO: Should this be targeted against guid for uniqueness? Creating a knowledge skill in career always generates a new SkillId instead of using the one from skills.
            XmlNode skillNode = XmlManager.Load("skills.xml").SelectSingleNode($"chummer/knowledgeskills/skill[name = \"{name}\"]");
            if (skillNode != null)
            {
                _type = skillNode["category"]?.InnerText ?? string.Empty;
                AttributeObject = CharacterObject.GetAttribute(skillNode["attribute"]?.InnerText ?? "LOG");
            }
        }

        public override string SkillCategory
        {
            get { return Type; }
        }

        public override string DisplayPool
        {
            get
            {
                if (Rating == 0 && _type == "Language")
                {
                    return "N";
                }
                else
                {
                    return base.DisplayPool;
                }
            }
        }

        /// <summary>
        /// The attributeValue this skill have from Skilljacks + Knowsoft
        /// </summary>
        /// <returns>Artificial skill attributeValue</returns>
        public override int CyberwareRating()
        {

            if (CachedWareRating != int.MinValue)
                return CachedWareRating;

            if (IsKnowledgeSkill && CharacterObject.SkillsoftAccess)
            {
                Func<Gear, int> recusivestuff = null;
                recusivestuff = (gear) =>
                {
                    //TODO this works with translate?
                    if (gear.Equipped && gear.Category == "Skillsofts" &&
                        (gear.Extra == Name ||
                         gear.Extra == Name + ", " + LanguageManager.GetString("Label_SelectGear_Hacked")))
                    {
                        return gear.Rating;
                    }
                    return gear.Children.Select(child => recusivestuff(child)).FirstOrDefault(returned => returned > 0);
                };

                return CachedWareRating = CharacterObject.Gear.Select(child => recusivestuff(child)).FirstOrDefault(val => val > 0);
            }

            return CachedWareRating = 0;
        }

        public string Type
        {
            get { return _type; }
            set
            {
                string strNewAttributeValue;
                if (!CategoriesSkillMap.TryGetValue(value, out strNewAttributeValue)) return;
                AttributeObject.PropertyChanged -= OnLinkedAttributeChanged;
                AttributeObject = CharacterObject.GetAttribute(strNewAttributeValue);

                AttributeObject.PropertyChanged += OnLinkedAttributeChanged;
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AttributeModifiers));
            }
        }

        /// <summary>
        /// Things that modify the dicepool of the skill
        /// </summary>
        public override int PoolModifiers
        {
            get
            {
                int adj = _type == "Language" && CharacterObject.SkillsSection.Linguist ? 1 : 0;
                return base.PoolModifiers + adj + WoundModifier;
            }
        }

        /// <summary>
        /// How much karma this costs. Return value during career mode is undefined
        /// </summary>
        /// <returns></returns>
        public override int CurrentKarmaCost()
        {
            int intTotalBaseRating = TotalBaseRating;
            int cost = intTotalBaseRating * (intTotalBaseRating + 1);
            int lower = Base + FreeKarma();
            cost -= lower * (lower + 1);
            if (CharacterObject.Options.EducationQualitiesApplyOnChargenKarma && HasRelatedBoost())
            {
                cost -= Math.Max(intTotalBaseRating - Math.Max(2, lower), 0);
            }

            cost /= 2;
            cost *= CharacterObject.Options.KarmaImproveKnowledgeSkill;
            // We have bought the first level with karma, too
            if (lower == 0 && cost > 0)
                cost += CharacterObject.Options.KarmaNewKnowledgeSkill - CharacterObject.Options.KarmaImproveKnowledgeSkill;
            cost +=  //Spec
                    (!string.IsNullOrWhiteSpace(Specialization) && BuyWithKarma) ?
                    CharacterObject.Options.KarmaKnowledgeSpecialization : 0;

            if (UneducatedEffect()) cost *= 2;

            return Math.Max(cost, 0);
        }

        public static int CompareKnowledgeSkills(KnowledgeSkill rhs, KnowledgeSkill lhs)
        {
            if (rhs.Name != null && lhs.Name != null)
            {
                return string.Compare(rhs.WriteableName, lhs.WriteableName, StringComparison.Ordinal);
            }
            return 0;
        }

        /// <summary>
        /// Karma price to upgrade. Returns negative if impossible. Minimum value is always 1.
        /// </summary>
        /// <returns>Price in karma</returns>
        public override int UpgradeKarmaCost()
        {
            int intTotalBaseRating = TotalBaseRating;
            if (intTotalBaseRating >= RatingMaximum)
            {
                return -1;
            }
            int adjustment = 0;
            if (CharacterObject.SkillsSection.JackOfAllTrades && CharacterObject.Created)
            {
                adjustment = intTotalBaseRating >= 5 ? 2 : -1;
            }
            if (HasRelatedBoost() && CharacterObject.Created && intTotalBaseRating >= 2)
            {
                adjustment -= 1;
            }

            int value = intTotalBaseRating == 0 ?
                CharacterObject.Options.KarmaNewKnowledgeSkill + adjustment :
                (intTotalBaseRating + 1) * CharacterObject.Options.KarmaImproveKnowledgeSkill + adjustment;

            value = Math.Max(value, 1);
            if (UneducatedEffect())
                value *= 2;
            return value;
        }

        /// <summary>
        /// How much Sp this costs. Price during career mode is undefined
        /// </summary>
        /// <returns></returns>
        public override int CurrentSpCost()
        {
            int intPointCost = BasePoints + (string.IsNullOrWhiteSpace(Specialization) || BuyWithKarma ? 0 : 1);
            if (HasRelatedBoost())
            {
                intPointCost = (intPointCost + 1)/2;
            }
            return intPointCost;
        }

        /// <summary>
        /// This method checks if the character have the related knowledge skill
        /// quality.
        /// 
        /// Eg. If it is a language skill, it returns Character.Linguistic, if
        /// it is technical it returns Character.TechSchool
        /// </summary>
        /// <returns></returns>
        private bool HasRelatedBoost()
        {
            switch (_type)
            {
                case "Language":
                    return CharacterObject.SkillsSection.Linguist;
                case "Professional":
                    return CharacterObject.SkillsSection.TechSchool;
                case "Street":
                    return CharacterObject.SkillsSection.SchoolOfHardKnocks;
                case "Academic":
                    return CharacterObject.SkillsSection.CollegeEducation;
                case "Interest":
                    return false;
            }
            return false;
        }

        private bool UneducatedEffect()
        {
            switch (_type)
            {
                case "Professional":
                case "Academic":
                    return CharacterObject.SkillsSection.Uneducated;
            }
            return false;
        }

        protected override void SaveExtendedData(XmlTextWriter writer)
        {
            writer.WriteElementString("name", Name);
            writer.WriteElementString("type", _type);
            if (_translated != null)
                writer.WriteElementString(GlobalOptions.Language, _translated);
            if (ForcedName)
                writer.WriteElementString("forced", null);
        }

        public void Load(XmlNode node)
        {
            if (node == null)
                return;
            string strTemp = Name;
            if (node.TryGetStringFieldQuickly("name", ref strTemp))
                Name = strTemp;
            node.TryGetStringFieldQuickly(GlobalOptions.Language, ref _translated);

            LoadSuggestedSpecializations(Name);
            string strCategoryString = string.Empty;
            if ((node.TryGetStringFieldQuickly("type", ref strCategoryString) && !string.IsNullOrEmpty(strCategoryString))
                || (node.TryGetStringFieldQuickly("skillcategory", ref strCategoryString) && !string.IsNullOrEmpty(strCategoryString)))
            {
                Type = strCategoryString;
            }
        }

        public override bool IsKnowledgeSkill
        {
            get { return true; }
        }
    }
}
