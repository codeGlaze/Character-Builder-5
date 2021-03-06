﻿
using CB_5e.Services;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Xamarin.Forms;
using Character_Builder;
using System;
using System.Collections.Generic;
using iTextSharp.text.pdf;
using OGL.Keywords;
using System.Text;
using OGL.Features;
using System.Linq;
using OGL.Descriptions;
using OGL;
using System.IO;
using PCLStorage;
using OGL.Spells;
using OGL.Base;
using OGL.Items;
using CB_5e.Droid;
using iTextSharp.text;
using Android.Content;

[assembly: Dependency(typeof(PDF_Droid))]
namespace CB_5e.Droid
{
    public class PDF_Droid : IPDFService
    {
        public async Task ExportPDF(string Exporter, BuilderContext context, bool preserveEdit, bool includeResources, bool log, bool spell)
        {
            PDF p = await PDF.Load(await PCLSourceManager.Data.GetFileAsync(Exporter).ConfigureAwait(false)).ConfigureAwait(false);
            await p.ExportPDF(context, preserveEdit, includeResources, log, spell).ConfigureAwait(false);
        }
    }

    public class PDF
    {
        [XmlIgnore]
        private static XmlSerializer serializer = new XmlSerializer(typeof(PDF));
        public String Name { get; set; }
        public String File { get; set; }
        public String SpellFile { get; set; }
        public String LogFile { get; set; }
        public String SpellbookFile { get; set; }
        public List<PDFField> Fields = new List<PDFField>();
        public List<PDFField> SpellFields = new List<PDFField>();
        public List<PDFField> LogFields = new List<PDFField>();
        public List<PDFField> SpellbookFields = new List<PDFField>();
        public async static Task<PDF> Load(IFile file)
        {
            using (Stream s = await file.OpenAsync(PCLStorage.FileAccess.Read))
            {
                PDF p = (PDF)serializer.Deserialize(s);
                p.File = PCLImport.MakeRelativeFile(p.File);
                p.SpellFile = PCLImport.MakeRelativeFile(p.SpellFile);
                p.LogFile = PCLImport.MakeRelativeFile(p.LogFile);
                p.SpellbookFile = PCLImport.MakeRelativeFile(p.SpellbookFile);
                return p;
            }
        }
        public async Task ExportPDF(BuilderContext Context, bool preserveEdit, bool includeResources, bool log, bool spell)
        {
            using (FileStream fs = System.IO.File.OpenWrite(Path.Combine(Android.App.Application.Context.ExternalCacheDir.Path, Context.Player.Name + ".pdf")))
            {
                Dictionary<String, String> trans = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<String, String> spelltrans = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<String, String> logtrans = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Dictionary<String, String> booktrans = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (PDFField pf in Fields) trans.Add(pf.Name, pf.Field);
                foreach (PDFField pf in SpellFields) spelltrans.Add(pf.Name, pf.Field);
                foreach (PDFField pf in LogFields) logtrans.Add(pf.Name, pf.Field);
                foreach (PDFField pf in SpellbookFields) booktrans.Add(pf.Name, pf.Field);
                Dictionary<string, bool> hiddenfeats = Context.Player.HiddenFeatures.ToDictionary(f => f, f => true, StringComparer.OrdinalIgnoreCase);
                List<SpellcastingFeature> spellcasts = new List<SpellcastingFeature>(from f in Context.Player.GetFeatures() where f is SpellcastingFeature && ((SpellcastingFeature)f).SpellcastingID != "MULTICLASS" select (SpellcastingFeature)f);
                List<Spell> spellbook = new List<Spell>();
                using (MemoryStream ms = new MemoryStream())
                {
                    IFile file = await PCLSourceManager.Data.GetFileAsync(File).ConfigureAwait(false);
                    using (Stream readstream = await file.OpenAsync(PCLStorage.FileAccess.Read).ConfigureAwait(false))
                    {
                        PdfReader sheet = new PdfReader(readstream);
                        if (preserveEdit) sheet.RemoveUsageRights();
                        PdfStamper p = new PdfStamper(sheet, ms);
                        if (p != null)
                        {
                            FillBasicFields(Context, trans, p);
                            String attacks = "";
                            String resources = "";
                            if (trans.ContainsKey("Resources"))
                            {
                                resources = String.Join("\n", Context.Player.GetResourceInfo(includeResources).Values);
                            }
                            else
                            {
                                if (attacks != "") attacks += "\n";
                                attacks += String.Join("\n", Context.Player.GetResourceInfo(includeResources).Values);
                            }
                            List<ModifiedSpell> bonusspells = new List<ModifiedSpell>(Context.Player.GetBonusSpells());
                            if (includeResources)
                            {
                                Dictionary<string, int> bsres = Context.Player.GetResources();
                                foreach (ModifiedSpell mods in bonusspells)
                                {
                                    mods.includeResources = true;
                                    if (bsres.ContainsKey(mods.getResourceID())) mods.used = bsres[mods.getResourceID()];
                                }
                            }
                            else
                            {
                                foreach (ModifiedSpell mods in bonusspells)
                                {
                                    mods.includeResources = false;
                                }
                            }
                            spellbook.AddRange(bonusspells);
                            if (trans.ContainsKey("BonusSpells"))
                            {
                                p.AcroFields.SetField(trans["BonusSpells"], String.Join("\n", bonusspells));
                            }
                            else if (trans.ContainsKey("Resources"))
                            {
                                if (resources != "") resources += "\n";
                                resources += String.Join("\n", bonusspells);
                            }
                            else
                            {
                                if (attacks != "") attacks += "\n";
                                attacks += String.Join("\n", bonusspells);
                            }

                            if (trans.ContainsKey("Resources"))
                            {
                                p.AcroFields.SetField(trans["Resources"], resources);
                            }
                            if (trans.ContainsKey("Attacks")) p.AcroFields.SetField(trans["Attacks"], attacks);
                            List<HitDie> hd = Context.Player.GetHitDie();
                            if (trans.ContainsKey("HitDieTotal")) p.AcroFields.SetField(trans["HitDieTotal"], String.Join(", ", from h in hd select h.Total()));
                            int maxhp = Context.Player.GetHitpointMax();
                            if (trans.ContainsKey("MaxHP")) p.AcroFields.SetField(trans["MaxHP"], maxhp.ToString());
                            if (includeResources)
                            {
                                if (trans.ContainsKey("CurrentHP")) p.AcroFields.SetField(trans["CurrentHP"], (maxhp + Context.Player.CurrentHPLoss).ToString());
                                if (trans.ContainsKey("TempHP")) p.AcroFields.SetField(trans["TempHP"], Context.Player.TempHP.ToString());
                                for (int d = 1; d <= Context.Player.FailedDeathSaves; d++) if (trans.ContainsKey("DeathSaveFail" + d)) p.AcroFields.SetField(trans["DeathSaveFail" + d], "Yes");
                                    else break;
                                for (int d = 1; d <= Context.Player.SuccessDeathSaves; d++) if (trans.ContainsKey("DeathSaveSuccess" + d)) p.AcroFields.SetField(trans["DeathSaveSuccess" + d], "Yes");
                                    else break;
                                if (trans.ContainsKey("HitDie")) p.AcroFields.SetField(trans["HitDie"], String.Join(", ", hd));
                                if (trans.ContainsKey("Inspiration")) if (Context.Player.Inspiration) p.AcroFields.SetField(trans["Inspiration"], "Yes");
                            }
                            if (Context.Player.Portrait != null)
                            {
                                float[] pos = p.AcroFields.GetFieldPositions(trans["Portrait"]);
                                for (int i = 0; i < pos.Length - 4; i += 5)
                                {
                                    iTextSharp.text.Image img = iTextSharp.text.Image.GetInstance(Context.Player.Portrait);
                                    img.ScaleToFit(Math.Abs(pos[i + 1] - pos[3]), Math.Abs(pos[i + 2] - pos[i + 4]));
                                    img.SetAbsolutePosition(pos[i + 1] + Math.Abs(pos[i + 1] - pos[i + 3]) / 2 - img.ScaledWidth / 2, pos[i + 2] + Math.Abs(pos[i + 2] - pos[i + 4]) / 2 - img.ScaledHeight / 2);
                                    p.GetOverContent((int)pos[i]).AddImage(img);
                                }
                            }
                            if (Context.Player.FactionImage != null)
                            {
                                float[] pos = p.AcroFields.GetFieldPositions(trans["FactionPortrait"]);
                                for (int i = 0; i < pos.Length - 4; i += 5)
                                {
                                    iTextSharp.text.Image img = iTextSharp.text.Image.GetInstance(Context.Player.FactionImage);
                                    img.ScaleToFit(Math.Abs(pos[i + 1] - pos[3]), Math.Abs(pos[i + 2] - pos[i + 4]));
                                    img.SetAbsolutePosition(pos[i + 1] + Math.Abs(pos[i + 1] - pos[i + 3]) / 2 - img.ScaledWidth / 2, pos[i + 2] + Math.Abs(pos[i + 2] - pos[i + 4]) / 2 - img.ScaledHeight / 2);
                                    p.GetOverContent((int)pos[i]).AddImage(img);
                                }
                            }
                            foreach (Skill s in Context.Skills.Values) if (trans.ContainsKey("Passive" + s.Name)) p.AcroFields.SetField(trans["Passive" + s.Name], Context.Player.GetPassiveSkill(s).ToString());
                            foreach (Skill s in Context.Player.GetSkillProficiencies()) p.AcroFields.SetField(trans[s.Name + "Proficiency"], "Yes");
                            foreach (SkillInfo s in Context.Player.GetSkills()) p.AcroFields.SetField(trans[s.Skill.Name], PlusMinus(s.Roll));
                            Ability saveprof = Context.Player.GetSaveProficiencies();
                            if (saveprof.HasFlag(Ability.Strength)) p.AcroFields.SetField(trans["StrengthSaveProficiency"], "Yes");
                            if (saveprof.HasFlag(Ability.Dexterity)) p.AcroFields.SetField(trans["DexteritySaveProficiency"], "Yes");
                            if (saveprof.HasFlag(Ability.Constitution)) p.AcroFields.SetField(trans["ConstitutionSaveProficiency"], "Yes");
                            if (saveprof.HasFlag(Ability.Intelligence)) p.AcroFields.SetField(trans["IntelligenceSaveProficiency"], "Yes");
                            if (saveprof.HasFlag(Ability.Wisdom)) p.AcroFields.SetField(trans["WisdomSaveProficiency"], "Yes");
                            if (saveprof.HasFlag(Ability.Charisma)) p.AcroFields.SetField(trans["CharismaSaveProficiency"], "Yes");
                            foreach (KeyValuePair<Ability, int> v in Context.Player.GetSaves()) p.AcroFields.SetField(trans[v.Key.ToString("F") + "Save"], PlusMinus(v.Value));
                            List<String> profs = new List<string>();
                            profs.Add(String.Join(", ", Context.Player.GetLanguages()));
                            profs.Add(String.Join(", ", Context.Player.GetToolProficiencies()));
                            profs.Add(String.Join("; ", Context.Player.GetToolKWProficiencies()));
                            //                        foreach (List<Keyword> kws in Context.Player.getToolKWProficiencies()) profs.Add("Any "+String.Join(", ", kws));
                            profs.Add(String.Join(", ", Context.Player.GetOtherProficiencies()));
                            profs.RemoveAll(t => t == "");
                            p.AcroFields.SetField(trans["Proficiencies"], String.Join("\n", profs));
                            Price money = Context.Player.GetMoney();
                            int level = Context.Player.GetLevel();
                            // TODO Proper Items:
                            List<Possession> equip = new List<Possession>();
                            List<Possession> treasure = new List<Possession>();
                            List<Feature> onUse = new List<Feature>();
                            foreach (Possession pos in Context.Player.GetItemsAndPossessions())
                            {
                                if (pos.BaseItem != null && pos.BaseItem != "")
                                {
                                    Item i = Context.GetItem(pos.BaseItem, null);
                                    if (pos.Equipped != EquipSlot.None || i is Weapon || i is Armor || i is Shield) equip.Add(pos);
                                    else treasure.Add(pos);
                                }
                                else treasure.Add(pos);
                                onUse.AddRange(pos.CollectOnUse(level, Context.Player, Context));
                            }
                            equip.Sort(delegate (Possession t1, Possession t2)
                            {
                                if (t1.Hightlight && !t2.Hightlight) return -1;
                                else if (t2.Hightlight && !t1.Hightlight) return 1;
                                else
                                {
                                    if (!string.Equals(t1.Equipped, EquipSlot.None, StringComparison.InvariantCultureIgnoreCase) && string.Equals(t2.Equipped, EquipSlot.None, StringComparison.InvariantCultureIgnoreCase)) return -1;
                                    else if (!string.Equals(t2.Equipped, EquipSlot.None, StringComparison.InvariantCultureIgnoreCase) && string.Equals(t1.Equipped, EquipSlot.None, StringComparison.InvariantCultureIgnoreCase)) return 1;
                                    else return (t1.ToString().CompareTo(t2.ToString()));
                                }

                            });
                            if (trans.ContainsKey("CP"))
                            {
                                if (trans.ContainsKey("Equipment")) p.AcroFields.SetField(trans["Equipment"], String.Join("\n", equip));
                                p.AcroFields.SetField(trans["CP"], money.cp.ToString());
                                if (trans.ContainsKey("SP")) p.AcroFields.SetField(trans["SP"], money.sp.ToString());
                                if (trans.ContainsKey("EP")) p.AcroFields.SetField(trans["EP"], money.ep.ToString());
                                if (trans.ContainsKey("GP")) p.AcroFields.SetField(trans["GP"], money.gp.ToString());
                                if (trans.ContainsKey("PP")) p.AcroFields.SetField(trans["PP"], money.pp.ToString());
                            }
                            else if (trans.ContainsKey("GP"))
                            {
                                if (trans.ContainsKey("Equipment")) p.AcroFields.SetField(trans["Equipment"], String.Join("\n", equip));
                                p.AcroFields.SetField(trans["GP"], money.ToGold());
                            }
                            else if (trans.ContainsKey("Equipment"))
                            {
                                p.AcroFields.SetField(trans["Equipment"], String.Join("\n", equip) + "\n" + money.ToString());
                                //TODO .CollectOnUseFeatures() foreach Possession
                            }
                            int chigh = 1;
                            foreach (SpellcastingFeature scf in spellcasts)
                            {
                                Spellcasting sc = Context.Player.GetSpellcasting(scf.SpellcastingID);
                                if (sc.Highlight != null && sc.Highlight != "")
                                {
                                    foreach (Spell s in sc.GetLearned(Context.Player, Context))
                                    {
                                        if (s.Name.ToLowerInvariant() == sc.Highlight.ToLowerInvariant())
                                        {
                                            if (trans.ContainsKey("Attack" + chigh))
                                            {
                                                AttackInfo ai = Context.Player.GetAttack(s, scf.SpellcastingAbility);
                                                p.AcroFields.SetField(trans["Attack" + chigh], s.Name);
                                                if (ai.SaveDC != "") p.AcroFields.SetField(trans["Attack" + chigh + "Attack"], "DC " + ai.SaveDC);
                                                else p.AcroFields.SetField(trans["Attack" + chigh + "Attack"], PlusMinus(ai.AttackBonus));
                                                if (trans.ContainsKey("Attack" + chigh + "DamageType"))
                                                {
                                                    p.AcroFields.SetField(trans["Attack" + chigh + "Damage"], ai.Damage);
                                                    p.AcroFields.SetField(trans["Attack" + chigh + "DamageType"], ai.DamageType);
                                                }
                                                else
                                                {
                                                    p.AcroFields.SetField(trans["Attack" + chigh + "Damage"], ai.Damage + " " + ai.DamageType);
                                                }
                                                chigh++;
                                            }
                                        }
                                    }
                                }
                            }
                            foreach (ModifiedSpell s in bonusspells)
                            {
                                if (Utils.Matches(Context, s, "Attack or Save", null))
                                {
                                    if (trans.ContainsKey("Attack" + chigh))
                                    {
                                        AttackInfo ai = Context.Player.GetAttack(s, s.differentAbility);
                                        p.AcroFields.SetField(trans["Attack" + chigh], s.Name);
                                        if (ai.SaveDC != "") p.AcroFields.SetField(trans["Attack" + chigh + "Attack"], "DC " + ai.SaveDC);
                                        else p.AcroFields.SetField(trans["Attack" + chigh + "Attack"], PlusMinus(ai.AttackBonus));
                                        if (trans.ContainsKey("Attack" + chigh + "DamageType"))
                                        {
                                            p.AcroFields.SetField(trans["Attack" + chigh + "Damage"], ai.Damage);
                                            p.AcroFields.SetField(trans["Attack" + chigh + "DamageType"], ai.DamageType);
                                        }
                                        else
                                        {
                                            p.AcroFields.SetField(trans["Attack" + chigh + "Damage"], ai.Damage + " " + ai.DamageType);
                                        }
                                        chigh++;
                                    }
                                }
                            }
                            foreach (Possession pos in equip)
                            {
                                AttackInfo ai = Context.Player.GetAttack(pos);
                                if (ai != null)
                                {
                                    if (trans.ContainsKey("Attack" + chigh))
                                    {
                                        p.AcroFields.SetField(trans["Attack" + chigh], pos.ToString());
                                        if (ai.SaveDC != "") p.AcroFields.SetField(trans["Attack" + chigh + "Attack"], "DC " + ai.SaveDC);
                                        else p.AcroFields.SetField(trans["Attack" + chigh + "Attack"], PlusMinus(ai.AttackBonus));
                                        if (trans.ContainsKey("Attack" + chigh + "DamageType"))
                                        {
                                            p.AcroFields.SetField(trans["Attack" + chigh + "Damage"], ai.Damage);
                                            p.AcroFields.SetField(trans["Attack" + chigh + "DamageType"], ai.DamageType);
                                        }
                                        else
                                        {
                                            p.AcroFields.SetField(trans["Attack" + chigh + "Damage"], ai.Damage + " " + ai.DamageType);
                                        }
                                        chigh++;
                                    }
                                }
                            }
                            if (trans.ContainsKey("Treasure")) p.AcroFields.SetField(trans["Treasure"], String.Join("\n", treasure));
                            if (preserveEdit || true)
                            {
                                if (trans.ContainsKey("RaceBackgroundFeatures"))
                                {
                                    List<string> feats = new List<string>();
                                    foreach (Feature f in onUse) if (!f.Hidden) feats.Add(f.ShortDesc());
                                    foreach (Feature f in Context.Player.GetBackgroundFeatures()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats.Add(f.ShortDesc());
                                    foreach (Feature f in Context.Player.GetRaceFeatures()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats.Add(f.ShortDesc());
                                    p.AcroFields.SetField(trans["RaceBackgroundFeatures"], String.Join("\n", feats));
                                    List<string> feats2 = new List<string>();
                                    foreach (Feature f in Context.Player.GetClassFeatures()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats2.Add(f.ShortDesc());
                                    foreach (Feature f in Context.Player.GetCommonFeaturesAndFeats()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats2.Add(f.ShortDesc());
                                    if (trans.ContainsKey("Features")) p.AcroFields.SetField(trans["Features"], String.Join("\n", feats2));
                                }
                                else if (trans.ContainsKey("Features"))
                                {
                                    List<string> feats = new List<string>();
                                    foreach (Feature f in onUse) if (!f.Hidden) feats.Add(f.ShortDesc());
                                    foreach (Feature f in Context.Player.GetBackgroundFeatures()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats.Add(f.ShortDesc());
                                    foreach (Feature f in Context.Player.GetRaceFeatures()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats.Add(f.ShortDesc());
                                    foreach (Feature f in Context.Player.GetClassFeatures()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats.Add(f.ShortDesc());
                                    foreach (Feature f in Context.Player.GetCommonFeaturesAndFeats()) if (!f.Hidden && !hiddenfeats.ContainsKey(f.Name)) feats.Add(f.ShortDesc());
                                    p.AcroFields.SetField(trans["Features"], String.Join("\n", feats));
                                }
                            }
                            else
                            {
                                var helveticaBold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 6);
                                var helveticaRegular = FontFactory.GetFont(FontFactory.HELVETICA, 6);
                                Phrase feats = new Phrase();
                                foreach (Feature f in Context.Player.GetBackgroundFeatures()) if (!f.Hidden)
                                    {
                                        feats.Add(new Chunk(f.Name + ": ", helveticaBold));
                                        feats.Add(new Chunk(f.Text + "\n", helveticaRegular));
                                    }
                                foreach (Feature f in Context.Player.GetRaceFeatures()) if (!f.Hidden)
                                    {
                                        feats.Add(new Chunk(f.Name + ": ", helveticaBold));
                                        feats.Add(new Chunk(f.Text + "\n", helveticaRegular));
                                    }
                                float[] rbpos = p.AcroFields.GetFieldPositions(trans["RaceBackgroundFeatures"]);
                                for (int i = 0; i < rbpos.Length - 4; i += 5)
                                {
                                    ColumnText currentColumnText = new ColumnText(p.GetOverContent((int)rbpos[i]));
                                    currentColumnText.SetSimpleColumn(feats, rbpos[i + 1], rbpos[i + 2], rbpos[i + 3], rbpos[i + 4], 13, iTextSharp.text.Element.ALIGN_LEFT);
                                    currentColumnText.Go();
                                }
                                Phrase feats2 = new Phrase();
                                foreach (Feature f in Context.Player.GetClassFeatures()) if (!f.Hidden)
                                    {
                                        feats.Add(new Chunk(f.Name + ": ", helveticaBold));
                                        feats.Add(new Chunk(f.Text + "\n", helveticaRegular));
                                    }
                                foreach (Feature f in Context.Player.GetCommonFeaturesAndFeats()) if (!f.Hidden)
                                    {
                                        feats.Add(new Chunk(f.Name + ": ", helveticaBold));
                                        feats.Add(new Chunk(f.Text + "\n", helveticaRegular));
                                    }
                                float[] pos = p.AcroFields.GetFieldPositions(trans["Features"]);
                                for (int i = 0; i < pos.Length - 4; i += 5)
                                {
                                    ColumnText currentColumnText = new ColumnText(p.GetOverContent((int)pos[i]));
                                    currentColumnText.SetSimpleColumn(feats, rbpos[i + 1], rbpos[i + 2], rbpos[i + 3], rbpos[i + 4], 13, iTextSharp.text.Element.ALIGN_LEFT);
                                    currentColumnText.Go();
                                }
                            }
                            p.FormFlattening = !preserveEdit;
                            p.Writer.CloseStream = false;
                            p.Close();
                        }
                    }
                    PdfReader x = new PdfReader(ms.ToArray());
                    if (preserveEdit) x.RemoveUsageRights();
                    Document sourceDocument = new Document(x.GetPageSizeWithRotation(1));
                    PdfCopy pdfCopy = new PdfCopy(sourceDocument, fs);
                    if (pdfCopy != null)
                    {
                        sourceDocument.Open();
                        for (int i = 1; i <= x.NumberOfPages; i++)
                        {
                            pdfCopy.SetPageSize(x.GetPageSize(i));
                            pdfCopy.AddPage(pdfCopy.GetImportedPage(x, i));
                        }
                        IFile spfile = await PCLSourceManager.Data.GetFileAsync(SpellFile).ConfigureAwait(false);
                        foreach (SpellcastingFeature scf in spellcasts)
                        {
                            if (scf.SpellcastingID != "MULTICLASS")
                            {
                                Spellcasting sc = Context.Player.GetSpellcasting(scf.SpellcastingID);
                                int classlevel = Context.Player.GetClassLevel(scf.SpellcastingID);
                                List<int> SpellSlots = Context.Player.GetSpellSlots(scf.SpellcastingID);
                                List<int> UsedSpellSlots = Context.Player.GetUsedSpellSlots(scf.SpellcastingID);
                                List<Spell> Available = new List<Spell>();
                                List<Spell> Prepared = new List<Spell>();
                                if (scf.Preparation == PreparationMode.ClassList)
                                {
                                    Available.AddRange(sc.GetAdditionalClassSpells(Context.Player, Context));
                                    Available.AddRange(Utils.FilterSpell(Context, scf.PrepareableSpells, scf.SpellcastingID, classlevel));
                                    Prepared.AddRange(sc.GetPrepared(Context.Player, Context));
                                }
                                else if (scf.Preparation == PreparationMode.Spellbook)
                                {
                                    Available.AddRange(sc.GetSpellbook(Context.Player, Context));
                                    Prepared.AddRange(sc.GetPrepared(Context.Player, Context));
                                }
                                else
                                {
                                    Prepared.AddRange(sc.GetPrepared(Context.Player, Context));
                                }
                                Prepared.AddRange(sc.GetLearned(Context.Player, Context));
                                Available.AddRange(Prepared);
                                spellbook.AddRange(Available);
                                List<Spell> Shown = new List<Spell>(Available.Distinct());
                                Shown.Sort(delegate (Spell t1, Spell t2)
                                {
                                    bool t1p = Prepared.Contains(t1);
                                    bool t2p = Prepared.Contains(t2);
                                    if (t1p && t2p) return (t1.Name.CompareTo(t2.Name));
                                    else if (t1p) return -1;
                                    else if (t2p) return 1;
                                    else return (t1.Name.CompareTo(t2.Name));

                                });
                                List<LinkedList<Spell>> SpellLevels = new List<LinkedList<Spell>>();
                                foreach (Spell s in Shown)
                                {
                                    while (SpellLevels.Count <= s.Level) SpellLevels.Add(new LinkedList<Spell>());
                                    SpellLevels[s.Level].AddLast(s);
                                }
                                while (SpellLevels.Count <= SpellSlots.Count) SpellLevels.Add(new LinkedList<Spell>());


                                int sheetmaxlevel = 0;
                                for (sheetmaxlevel = 1; sheetmaxlevel < SpellLevels.Count; sheetmaxlevel++) if (!spelltrans.ContainsKey("Spell" + sheetmaxlevel + "-1")) break;
                                sheetmaxlevel--;
                                int offset = 0;

                                while (SpellLevels.Count > 0 && sheetmaxlevel > 0)
                                {

                                    using (Stream sps = await spfile.OpenAsync(PCLStorage.FileAccess.Read).ConfigureAwait(false))
                                    {
                                        PdfReader spellsheet = new PdfReader(sps);
                                        if (preserveEdit) spellsheet.RemoveUsageRights();
                                        using (MemoryStream sms = new MemoryStream())
                                        {
                                            PdfStamper sp = new PdfStamper(spellsheet, sms);
                                            if (sp != null)
                                            {
                                                FillBasicFields(Context, spelltrans, sp);
                                                if (spelltrans.ContainsKey("SpellcastingClass")) sp.AcroFields.SetField(spelltrans["SpellcastingClass"], scf.DisplayName);
                                                if (spelltrans.ContainsKey("SpellcastingAbility")) sp.AcroFields.SetField(spelltrans["SpellcastingAbility"], Enum.GetName(typeof(Ability), scf.SpellcastingAbility));
                                                if (spelltrans.ContainsKey("SpellSaveDC")) sp.AcroFields.SetField(spelltrans["SpellSaveDC"], Context.Player.GetSpellSaveDC(scf.SpellcastingID, scf.SpellcastingAbility).ToString());
                                                if (spelltrans.ContainsKey("SpellAttackBonus")) sp.AcroFields.SetField(spelltrans["SpellAttackBonus"], PlusMinus(Context.Player.GetSpellAttack(scf.SpellcastingID, scf.SpellcastingAbility)));
                                                for (int i = 0; i <= sheetmaxlevel && i < SpellLevels.Count; i++)
                                                {
                                                    if (SpellSlots.Count >= i && i > 0 && SpellSlots[i - 1] > 0)
                                                    {
                                                        if (spelltrans.ContainsKey("SpellSlots" + i))
                                                        {
                                                            sp.AcroFields.SetField(spelltrans["SpellSlots" + i], (offset > 0 ? "(" + (offset + i) + ") " : "") + SpellSlots[i - 1].ToString());
                                                        }
                                                    }
                                                    if (includeResources && UsedSpellSlots.Count >= i && i > 0)
                                                    {
                                                        if (spelltrans.ContainsKey("SpellSlotsExpended" + i))
                                                        {
                                                            sp.AcroFields.SetField(spelltrans["SpellSlotsExpended" + i], UsedSpellSlots[i - 1].ToString());
                                                        }
                                                    }
                                                    int field = 1;
                                                    if (!spelltrans.ContainsKey("Spell" + i + "-1")) SpellLevels[i].Clear();
                                                    while (SpellLevels[i].Count > 0)
                                                    {
                                                        if (!spelltrans.ContainsKey("Spell" + i + "-" + field)) break;
                                                        sp.AcroFields.SetField(spelltrans["Spell" + i + "-" + field], SpellLevels[i].First.Value.Name);
                                                        if (Prepared.Contains(SpellLevels[i].First.Value) && spelltrans.ContainsKey("Prepared" + i + "-" + field))
                                                            sp.AcroFields.SetField(spelltrans["Prepared" + i + "-" + field], "Yes");
                                                        SpellLevels[i].RemoveFirst();
                                                        field++;
                                                    }
                                                }
                                                sp.FormFlattening = !preserveEdit;
                                                sp.Writer.CloseStream = false;
                                                sp.Close();
                                            }
                                            PdfReader sx = new PdfReader(sms.ToArray());
                                            for (int si = 1; si <= sx.NumberOfPages; si++)
                                            {
                                                pdfCopy.SetPageSize(sx.GetPageSize(si));
                                                pdfCopy.AddPage(pdfCopy.GetImportedPage(sx, si));
                                            }
                                        }
                                        spellsheet.Close();
                                    }
                                    bool empty = true;
                                    for (int i = 0; i <= sheetmaxlevel && i < SpellLevels.Count; i++) if (SpellLevels[i].Count > 0) empty = false;
                                    if (empty)
                                    {
                                        SpellLevels.RemoveRange(1, sheetmaxlevel);
                                        if (SpellLevels.Count == 1) SpellLevels.Clear(); //Cantrips
                                        offset += sheetmaxlevel;
                                    }

                                }
                            }

                        }

                        if (log && LogFile != null && LogFile != "")
                        {
                            Queue<JournalEntry> entries = new Queue<JournalEntry>(Context.Player.ComplexJournal);

                            IFile lfile = await PCLSourceManager.Data.GetFileAsync(LogFile).ConfigureAwait(false);

                            Price gold = Context.Player.GetMoney(false);
                            int xp = Context.Player.XP;
                            int renown = 0;
                            int downtime = 0;
                            int magic = 0;
                            int sheet = 0;
                            while (entries.Count > 0)
                            {
                                using (Stream ls = await lfile.OpenAsync(PCLStorage.FileAccess.Read).ConfigureAwait(false))
                                {
                                    PdfReader logsheet = new PdfReader(ls);
                                    if (preserveEdit) logsheet.RemoveUsageRights();
                                    using (MemoryStream lms = new MemoryStream())
                                    {
                                        int counter = 1;
                                        PdfStamper lp = new PdfStamper(logsheet, lms);
                                        if (lp != null)
                                        {
                                            sheet++;
                                            FillBasicFields(Context, logtrans, lp);
                                            if (logtrans.ContainsKey("Sheet")) lp.AcroFields.SetField(logtrans["Sheet"], sheet.ToString());
                                            while (entries.Count > 0 && (logtrans.ContainsKey("Title" + counter) || logtrans.ContainsKey("XP" + counter)))
                                            {
                                                JournalEntry entry = entries.Dequeue();
                                                if (entry.InSheet)
                                                {
                                                    if (logtrans.ContainsKey("Title" + counter)) lp.AcroFields.SetField(logtrans["Title" + counter], entry.Title);
                                                    if (logtrans.ContainsKey("Session" + counter)) lp.AcroFields.SetField(logtrans["Session" + counter], entry.Session);
                                                    if (logtrans.ContainsKey("Date" + counter)) lp.AcroFields.SetField(logtrans["Date" + counter], entry.Added.ToString());
                                                    if (logtrans.ContainsKey("DM" + counter)) lp.AcroFields.SetField(logtrans["DM" + counter], entry.DM);
                                                    if (entry.Text != null)
                                                    {
                                                        if (logtrans.ContainsKey("Notes" + counter)) lp.AcroFields.SetField(logtrans["Notes" + counter], entry.Text);
                                                        else if (logtrans.ContainsKey("Notes" + counter + "Line1"))
                                                        {
                                                            int line = 1;
                                                            Queue<string> lines = new Queue<string>(entry.Text.Split('\n'));
                                                            while (lines.Count > 0 && logtrans.ContainsKey("Notes" + counter + "Line" + (line + 1)))
                                                            {
                                                                lp.AcroFields.SetField(logtrans["Notes" + counter + "Line" + line], lines.Dequeue());
                                                                line++;
                                                            }
                                                            lp.AcroFields.SetField(logtrans["Notes" + counter + "Line" + line], string.Join(" ", lines));
                                                        }
                                                    }
                                                    if (logtrans.ContainsKey("XPStart" + counter)) lp.AcroFields.SetField(logtrans["XPStart" + counter], xp.ToString());
                                                    if (logtrans.ContainsKey("GoldStart" + counter)) lp.AcroFields.SetField(logtrans["GoldStart" + counter], gold.ToGold());
                                                    if (logtrans.ContainsKey("DowntimeStart" + counter)) lp.AcroFields.SetField(logtrans["DowntimeStart" + counter], downtime.ToString());
                                                    if (logtrans.ContainsKey("RenownStart" + counter)) lp.AcroFields.SetField(logtrans["RenownStart" + counter], renown.ToString());
                                                    if (logtrans.ContainsKey("MagicItemsStart" + counter)) lp.AcroFields.SetField(logtrans["MagicItemsStart" + counter], magic.ToString());

                                                    if (logtrans.ContainsKey("XP" + counter)) lp.AcroFields.SetField(logtrans["XP" + counter], entry.XP.ToString());
                                                    if (logtrans.ContainsKey("Gold" + counter)) lp.AcroFields.SetField(logtrans["Gold" + counter], entry.GetMoney());
                                                    if (logtrans.ContainsKey("Downtime" + counter)) lp.AcroFields.SetField(logtrans["Downtime" + counter], PlusMinus(entry.Downtime));
                                                    if (logtrans.ContainsKey("Renown" + counter)) lp.AcroFields.SetField(logtrans["Renown" + counter], PlusMinus(entry.Renown));
                                                    if (logtrans.ContainsKey("MagicItems" + counter)) lp.AcroFields.SetField(logtrans["MagicItems" + counter], PlusMinus(entry.MagicItems));
                                                }
                                                xp += entry.XP;
                                                gold.pp += entry.PP;
                                                gold.gp += entry.GP;
                                                gold.sp += entry.SP;
                                                gold.ep += entry.EP;
                                                gold.cp += entry.CP;
                                                renown += entry.Renown;
                                                magic += entry.MagicItems;
                                                downtime += entry.Downtime;
                                                if (entry.InSheet)
                                                {
                                                    if (logtrans.ContainsKey("XPEnd" + counter)) lp.AcroFields.SetField(logtrans["XPEnd" + counter], xp.ToString());
                                                    if (logtrans.ContainsKey("GoldEnd" + counter)) lp.AcroFields.SetField(logtrans["GoldEnd" + counter], gold.ToGold());
                                                    if (logtrans.ContainsKey("DowntimeEnd" + counter)) lp.AcroFields.SetField(logtrans["DowntimeEnd" + counter], downtime.ToString());
                                                    if (logtrans.ContainsKey("RenownEnd" + counter)) lp.AcroFields.SetField(logtrans["RenownEnd" + counter], renown.ToString());
                                                    if (logtrans.ContainsKey("MagicItemsEnd" + counter)) lp.AcroFields.SetField(logtrans["MagicItemsEnd" + counter], magic.ToString());
                                                    counter++;
                                                }

                                            }
                                            lp.FormFlattening = !preserveEdit;
                                            lp.Writer.CloseStream = false;
                                            lp.Close();
                                        }
                                        if (counter > 1)
                                        {
                                            PdfReader lx = new PdfReader(lms.ToArray());
                                            for (int li = 1; li <= lx.NumberOfPages; li++)
                                            {
                                                pdfCopy.SetPageSize(lx.GetPageSize(li));
                                                pdfCopy.AddPage(pdfCopy.GetImportedPage(lx, li));
                                            }
                                        }
                                    }
                                    logsheet.Close();
                                }
                            }

                        }

                        if (spell && SpellbookFile != null && SpellbookFile != "")
                        {
                            IFile sfile = await PCLSourceManager.Data.GetFileAsync(SpellbookFile).ConfigureAwait(false);
                            List<SpellModifyFeature> mods = (from f in Context.Player.GetFeatures() where f is SpellModifyFeature select f as SpellModifyFeature).ToList();
                            Queue<Spell> entries = new Queue<Spell>(spellbook.OrderBy(s => s.Name).Distinct(new SpellEqualityComparer()));
                            int sheet = 0;
                            while (entries.Count > 0)
                            {
                                
                                using (Stream ss = await sfile.OpenAsync(PCLStorage.FileAccess.Read).ConfigureAwait(false))
                                {
                                    PdfReader spellbooksheet = new PdfReader(ss);
                                    if (preserveEdit) spellbooksheet.RemoveUsageRights();
                                    using (MemoryStream sbms = new MemoryStream())
                                    {
                                        int counter = 1;
                                        PdfStamper sbp = new PdfStamper(spellbooksheet, sbms);
                                        if (sbp != null)
                                        {
                                            sheet++;
                                            FillBasicFields(Context, logtrans, sbp);
                                            if (booktrans.ContainsKey("Sheet")) sbp.AcroFields.SetField(booktrans["Sheet"], sheet.ToString());
                                            while (entries.Count > 0 && (booktrans.ContainsKey("Name" + counter) || booktrans.ContainsKey("Description" + counter)))
                                            {
                                                Spell entry = entries.Dequeue();
                                                StringBuilder description = new StringBuilder();
                                                String name = entry.Name;
                                                String level = "";
                                                List<Keyword> original = entry.GetKeywords();
                                                List<Keyword> keywords = new List<Keyword>(original);
                                                if (booktrans.ContainsKey("Level" + counter)) sbp.AcroFields.SetField(booktrans["Level" + counter], entry.Level.ToString());
                                                else level = (entry.Level == 0 ? "" : " " + AddOrdinal(entry.Level) + " Level");
                                                keywords.RemoveAll(k => k.Name.Equals("cantrip", StringComparison.InvariantCultureIgnoreCase));
                                                if (booktrans.ContainsKey("School" + counter)) sbp.AcroFields.SetField(booktrans["School" + counter], GetAndRemoveSchool(keywords));
                                                else if (!booktrans.ContainsKey("Keywords" + counter)) level += " " + GetAndRemoveSchool(keywords);
                                                if (entry.Level == 0) level += " Cantrip";
                                                if (booktrans.ContainsKey("SchoolLevel" + counter)) sbp.AcroFields.SetField(booktrans["SchoolLevel" + counter], level);
                                                else name += ", " + level;
                                                if (booktrans.ContainsKey("Name" + counter)) sbp.AcroFields.SetField(booktrans["Name" + counter], name);
                                                else description.Append(name).AppendLine();
                                                if (booktrans.ContainsKey("Classes" + counter)) sbp.AcroFields.SetField(booktrans["Classes" + counter], GetAndRemoveClasses(Context, keywords));
                                                if (booktrans.ContainsKey("Time" + counter)) sbp.AcroFields.SetField(booktrans["Time" + counter], entry.CastingTime);
                                                else description.Append("Casting Time: ").Append(entry.CastingTime).AppendLine();
                                                if (booktrans.ContainsKey("Range" + counter)) sbp.AcroFields.SetField(booktrans["Range" + counter], entry.Range);
                                                else description.Append("Range: ").Append(entry.Range).AppendLine();
                                                if (booktrans.ContainsKey("Components" + counter)) sbp.AcroFields.SetField(booktrans["Components" + counter], GetAndRemoveComponents(keywords));
                                                else if (!booktrans.ContainsKey("Keywords" + counter)) description.Append("Components: ").Append(GetAndRemoveComponents(keywords)).AppendLine();
                                                if (booktrans.ContainsKey("Duration" + counter)) sbp.AcroFields.SetField(booktrans["Duration" + counter], entry.Duration);
                                                else description.Append("Duration: ").Append(entry.Duration).AppendLine();

                                                if (booktrans.ContainsKey("Keywords" + counter)) sbp.AcroFields.SetField(booktrans["Keywords" + counter], String.Join(", ", keywords));

                                                description.Append(entry.Description);
                                                StringBuilder add = new StringBuilder();
                                                foreach (Description d in entry.Descriptions)
                                                {
                                                    add.Append(d.Name.ToUpperInvariant()).Append(": ").Append(d.Text).AppendLine();
                                                    if (d is ListDescription) foreach (Names n in (d as ListDescription).Names) add.Append(n.Title).Append(": ").Append(String.Join(", ", n.ListOfNames)).AppendLine();
                                                    if (d is TableDescription) foreach (TableEntry tr in (d as TableDescription).Entries) add.Append(tr.ToFullString()).AppendLine();
                                                }
                                                if (booktrans.ContainsKey("AdditionDescription" + counter)) sbp.AcroFields.SetField(booktrans["AdditionDescription" + counter], add.ToString());
                                                else description.AppendLine().AppendLine().Append(add.ToString());
                                                StringBuilder Modifiers = new StringBuilder();
                                                foreach (SpellModifyFeature m in mods.Where(f => Utils.Matches(Context, entry, ((SpellModifyFeature)f).Spells, null)))
                                                {
                                                    Modifiers.Append(m.Name.ToUpperInvariant()).Append(": ").Append(m.Text).AppendLine();
                                                }
                                                if (booktrans.ContainsKey("Modifiers" + counter)) sbp.AcroFields.SetField(booktrans["Modifiers" + counter], Modifiers.ToString());
                                                else description.AppendLine().Append(Modifiers.ToString());
                                                if (booktrans.ContainsKey("Source" + counter)) sbp.AcroFields.SetField(booktrans["Source" + counter], entry.Source);
                                                else description.AppendLine().Append("Source: ").Append(entry.Source).AppendLine();
                                                if (booktrans.ContainsKey("Description" + counter)) sbp.AcroFields.SetField(booktrans["Description" + counter], description.ToString());
                                                counter++;
                                            }
                                        }
                                        sbp.FormFlattening = !preserveEdit;
                                        sbp.Writer.CloseStream = false;
                                        sbp.Close();
                                        if (counter > 1)
                                        {
                                            PdfReader lx = new PdfReader(sbms.ToArray());
                                            for (int li = 1; li <= lx.NumberOfPages; li++)
                                            {
                                                pdfCopy.SetPageSize(lx.GetPageSize(li));
                                                pdfCopy.AddPage(pdfCopy.GetImportedPage(lx, li));
                                            }
                                        }

                                    }
                                    spellbooksheet.Close();
                                }
                            }
                        }
                        sourceDocument.Close();
                    }
                    pdfCopy.CloseStream = true;
                    pdfCopy.Close();
                }
            }
            var uri = Android.Net.Uri.Parse("file://" + Android.App.Application.Context.ExternalCacheDir.Path + "/" + Context.Player.Name + ".pdf");
            var intent = new Intent(Intent.ActionSend);
            intent.PutExtra(Intent.ExtraStream, uri);
            intent.SetType("application/pdf");
            intent.SetFlags(ActivityFlags.ClearWhenTaskReset | ActivityFlags.NewTask);
            Forms.Context.StartActivity(Intent.CreateChooser(intent, "Select App"));
        }

        private static List<Keyword> Schools = new List<Keyword>()
        {
            new Keyword("Abjuration"), new Keyword("Conjuration"), new Keyword("Divination"), new Keyword("Enchantment"), new Keyword("Evocation"), new Keyword("Illusion"), new Keyword("Necromancy"), new Keyword("Transmutation")
        };

        private string GetAndRemoveSchool(List<Keyword> kw)
        {
            List<string> s = new List<string>();
            foreach (Keyword k in Schools) if (kw.Remove(k)) s.Add(k.Name.ToLowerInvariant());
            string res = string.Join(", ", s);
            return char.ToUpper(res[0]) + res.Substring(1);
        }

        private string GetAndRemoveComponents(List<Keyword> kw)
        {
            List<string> r = new List<string>();
            bool v = false, s = false, m = false;
            string mat = "";
            for (int i = kw.Count - 1; i >= 0; i--)
            {
                if (kw[i].Name.Equals("verbal", StringComparison.InvariantCultureIgnoreCase))
                {
                    v = true;
                    kw.RemoveAt(i);
                } else if (kw[i].Name.Equals("somatic", StringComparison.InvariantCultureIgnoreCase))
                {
                    s = true;
                    kw.RemoveAt(i);
                } else  if (kw[i] is Material)
                {
                    m = true;
                    mat = mat == "" ? (kw[i] as Material).Components : mat + "; " + (kw[i] as Material).Components;
                    kw.RemoveAt(i);
                }
            }
            if (v) r.Add("V");
            if (s) r.Add("S");
            if (m) r.Add("M(" + mat + ")");
            string res = string.Join(", ", r);
            return char.ToUpper(res[0]) + res.Substring(1);
        }

        private static List<Keyword> Classes = new List<Keyword>();
        private string GetAndRemoveClasses(BuilderContext Context, List<Keyword> kw)
        {
            if (Classes.Count == 0) foreach (string cl in Context.ClassesSimple.Keys) Classes.Add(new Keyword(cl));
            List<string> s = new List<string>();
            foreach (Keyword k in Classes) if (kw.Remove(k)) s.Add(char.ToUpper(k.Name[0]) + k.Name.Substring(1).ToLowerInvariant());
            return string.Join(", ", s);
        }

        private static string AddOrdinal(int num)
        {
            if (num <= 0) return num.ToString();

            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }

            switch (num % 10)
            {
                case 1:
                    return num + "st";
                case 2:
                    return num + "nd";
                case 3:
                    return num + "rd";
                default:
                    return num + "th";
            }

        }

        private void FillBasicFields(BuilderContext Context, Dictionary<string, string> trans, PdfStamper p)
        {
            if (trans.ContainsKey("Background")) p.AcroFields.SetField(trans["Background"], Context.Player.BackgroundName);
            if (trans.ContainsKey("Race")) p.AcroFields.SetField(trans["Race"], Context.Player.GetRaceSubName());
            if (trans.ContainsKey("PersonalityTrait")) p.AcroFields.SetField(trans["PersonalityTrait"], Context.Player.PersonalityTrait);
            if (trans.ContainsKey("Ideal")) p.AcroFields.SetField(trans["Ideal"], Context.Player.Ideal);
            if (trans.ContainsKey("Bond")) p.AcroFields.SetField(trans["Bond"], Context.Player.Bond);
            if (trans.ContainsKey("Flaw")) p.AcroFields.SetField(trans["Flaw"], Context.Player.Flaw);
            if (trans.ContainsKey("PlayerName")) p.AcroFields.SetField(trans["PlayerName"], Context.Player.PlayerName);
            if (trans.ContainsKey("Alignment")) p.AcroFields.SetField(trans["Alignment"], Context.Player.Alignment);
            if (trans.ContainsKey("XP")) p.AcroFields.SetField(trans["XP"], Context.Player.GetXP().ToString());
            if (trans.ContainsKey("Age")) p.AcroFields.SetField(trans["Age"], Context.Player.Age.ToString());
            if (trans.ContainsKey("Height")) p.AcroFields.SetField(trans["Height"], Context.Player.Height.ToString());
            if (trans.ContainsKey("Weight")) p.AcroFields.SetField(trans["Weight"], Context.Player.Weight.ToString() + " lb");
            if (trans.ContainsKey("Eyes")) p.AcroFields.SetField(trans["Eyes"], Context.Player.Eyes.ToString());
            if (trans.ContainsKey("Skin")) p.AcroFields.SetField(trans["Skin"], Context.Player.Skin.ToString());
            if (trans.ContainsKey("Hair")) p.AcroFields.SetField(trans["Hair"], Context.Player.Hair.ToString());
            if (trans.ContainsKey("Speed")) p.AcroFields.SetField(trans["Speed"], Context.Player.GetSpeed().ToString());
            if (trans.ContainsKey("FactionName")) p.AcroFields.SetField(trans["FactionName"], Context.Player.FactionName);
            if (trans.ContainsKey("Backstory")) p.AcroFields.SetField(trans["Backstory"], Context.Player.Backstory);
            if (trans.ContainsKey("Allies")) p.AcroFields.SetField(trans["Allies"], Context.Player.Allies);
            if (trans.ContainsKey("Strength")) p.AcroFields.SetField(trans["Strength"], Context.Player.GetStrength().ToString());
            if (trans.ContainsKey("Dexterity")) p.AcroFields.SetField(trans["Dexterity"], Context.Player.GetDexterity().ToString());
            if (trans.ContainsKey("Constitution")) p.AcroFields.SetField(trans["Constitution"], Context.Player.GetConstitution().ToString());
            if (trans.ContainsKey("Intelligence")) p.AcroFields.SetField(trans["Intelligence"], Context.Player.GetIntelligence().ToString());
            if (trans.ContainsKey("Wisdom")) p.AcroFields.SetField(trans["Wisdom"], Context.Player.GetWisdom().ToString());
            if (trans.ContainsKey("Charisma")) p.AcroFields.SetField(trans["Charisma"], Context.Player.GetCharisma().ToString());
            if (trans.ContainsKey("StrengthModifier")) p.AcroFields.SetField(trans["StrengthModifier"], PlusMinus(Context.Player.GetStrengthMod()));
            if (trans.ContainsKey("DexterityModifier")) p.AcroFields.SetField(trans["DexterityModifier"], PlusMinus(Context.Player.GetDexterityMod()));
            if (trans.ContainsKey("ConstitutionModifier")) p.AcroFields.SetField(trans["ConstitutionModifier"], PlusMinus(Context.Player.GetConstitutionMod()));
            if (trans.ContainsKey("IntelligenceModifier")) p.AcroFields.SetField(trans["IntelligenceModifier"], PlusMinus(Context.Player.GetIntelligenceMod()));
            if (trans.ContainsKey("WisdomModifier")) p.AcroFields.SetField(trans["WisdomModifier"], PlusMinus(Context.Player.GetWisdomMod()));
            if (trans.ContainsKey("AC")) p.AcroFields.SetField(trans["AC"], Context.Player.GetAC().ToString());
            if (trans.ContainsKey("ProficiencyBonus")) p.AcroFields.SetField(trans["ProficiencyBonus"], PlusMinus(Context.Player.GetProficiency()));
            if (trans.ContainsKey("Initiative")) p.AcroFields.SetField(trans["Initiative"], PlusMinus(Context.Player.GetInitiative()));
            if (trans.ContainsKey("CharismaModifier")) p.AcroFields.SetField(trans["CharismaModifier"], Context.Player.GetCharismaMod().ToString());
            if (trans.ContainsKey("CharacterName")) p.AcroFields.SetField(trans["CharacterName"], Context.Player.Name);
            if (trans.ContainsKey("CharacterName2")) p.AcroFields.SetField(trans["CharacterName2"], Context.Player.Name);
            if (trans.ContainsKey("ClassLevel")) p.AcroFields.SetField(trans["ClassLevel"], String.Join(" | ", Context.Player.GetClassesStrings()));
            if (trans.ContainsKey("DCI")) p.AcroFields.SetField(trans["DCI"], Context.Player.DCI);
        }

        private string PlusMinus(int value)
        {
            if (value > 0) return "+" + value;
            if (value == 0) return "--";
            return value.ToString();
        }
    }
}
