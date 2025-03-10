﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ARKBreedingStats
{
    public partial class Form1 : Form
    {
        private List<string> creatures = new List<string>();
        private string[] statNames = new string[] { "Health", "Stamina", "Oxygen", "Food", "Weight", "Damage", "Speed", "Torpor" };
        private List<List<double[]>> stats = new List<List<double[]>>();
        private List<int> levelXP = new List<int>();
        private List<StatIO> statIOs = new List<StatIO>();
        private List<List<double[]>> results = new List<List<double[]>>();
        private int c = 0; // current creature
        private bool postTamed = false;
        private int activeStat = -1;
        private List<int> statsWithEff = new List<int>();
        private List<int> chosenResults = new List<int>();
        private int[] precisions = new int[] { 1, 1, 1, 1, 1, 3, 3, 1 }; // damage and speed are percentagevalues, need more precision
        private int[] levelDomFromTorporAndTotalRange = new int[] { 0, 0 }, levelWildFromTorporRange = new int[] { 0, 0 }; // 0: min, 1: max
        private bool[] activeStats = new bool[] { true, true, true, true, true, true, true, true };

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            statIOs.Add(this.statIOHealth);
            statIOs.Add(this.statIOStamina);
            statIOs.Add(this.statIOOxygen);
            statIOs.Add(this.statIOFood);
            statIOs.Add(this.statIOWeight);
            statIOs.Add(this.statIODamage);
            statIOs.Add(this.statIOSpeed);
            statIOs.Add(this.statIOTorpor);
            for (int s = 0; s < statNames.Length; s++)
            {
                statIOs[s].Title = statNames[s];
                if (precisions[s] == 3) { statIOs[s].Percent = true; }
            }
            loadFile(true);
            comboBoxCreatures.SelectedIndex = 0;
            labelSumDomSB.Text = "";
            ToolTip tt = new ToolTip();
            tt.SetToolTip(this.checkBoxOutputRowHeader, "Include Headerrow");
            tt.SetToolTip(this.checkBoxJustTamed, "Check this if there was no server-restart or if you didn't logout since you tamed the creature.\nUncheck this if you know there was a server-restart (many servers restart every night).\nIf it is some days ago (IRL) you tamed the creature you should probably uncheck this checkbox.");
        }

        private void clearAll()
        {
            results.Clear();
            statsWithEff.Clear();
            listBoxPossibilities.Items.Clear();
            chosenResults.Clear();
            for (int s = 0; s < 8; s++)
            {
                statIOs[s].Clear();
                chosenResults.Add(0);
                statIOs[s].BarLength = 0;
            }
            this.labelFootnote.Text = "";
            labelFootnote.BackColor = SystemColors.Control;
            this.numericUpDownLevel.BackColor = SystemColors.Window;
            this.numericUpDownLowerTEffBound.BackColor = SystemColors.Window;
            this.numericUpDownUpperTEffBound.BackColor = SystemColors.Window;
            this.checkBoxAlreadyBred.BackColor = System.Drawing.Color.Transparent;
            this.checkBoxJustTamed.BackColor = System.Drawing.Color.Transparent;
            panelSums.BackColor = SystemColors.Control;
            labelTE.BackColor = SystemColors.Control;
            buttonCopyClipboard.Enabled = false;
            activeStat = -1;
            labelTE.Text = "Extracted: n/a";
            labelSumDom.Text = "";
            labelSumWild.Text = "";
            labelSumWildSB.Text = "";
            for (int i = 0; i < 2; i++)
            {
                levelWildFromTorporRange[i] = 0;
                levelDomFromTorporAndTotalRange[i] = 0;
            }
        }

        private void buttonCalculate_Click(object sender, EventArgs e)
        {
            int activeStatKeeper = activeStat;
            clearAll();
            bool resultsValid = true;
            // torpor is directly proportional to wild level
            postTamed = (stats[c][7][0] + stats[c][7][0] * stats[c][7][1] * Math.Round((statIOs[7].Input - stats[c][7][0]) / (stats[c][7][0] * stats[c][7][1])) != statIOs[7].Input);

            // max level for wild according to torpor (possible bug ingame: torpor is depending on taming efficiency 5/3 - 2 times "too high" for level after taming until server-restart (not only the bonus levels are added, but also the existing levels again)
            double torporLevelTamingMultMax = 1, torporLevelTamingMultMin = 1;
            if (postTamed && this.checkBoxJustTamed.Checked)
            {
                torporLevelTamingMultMax = (200 + (double)this.numericUpDownUpperTEffBound.Value) / (400 + (double)this.numericUpDownUpperTEffBound.Value);
                torporLevelTamingMultMin = (200 + (double)this.numericUpDownLowerTEffBound.Value) / (400 + (double)this.numericUpDownLowerTEffBound.Value);
            }
            levelWildFromTorporRange[0] = (int)Math.Round((statIOs[7].Input - (postTamed ? stats[c][7][3] : 0) - stats[c][7][0]) * torporLevelTamingMultMin / (stats[c][7][0] * stats[c][7][1]), 0);
            levelWildFromTorporRange[1] = (int)Math.Round((statIOs[7].Input - (postTamed ? stats[c][7][3] : 0) - stats[c][7][0]) * torporLevelTamingMultMax / (stats[c][7][0] * stats[c][7][1]), 0);
            int[] levelDomRange = new int[] { 0, 0 };
            // lower/upper Bound of each stat (wild has no upper bound as wild-speed is unknown)
            if (postTamed)
            {
                for (int i = 0; i < 2; i++)
                {
                    levelDomRange[i] = (int)numericUpDownLevel.Value - levelWildFromTorporRange[1 - i] - 1; // creatures starts with level 1
                }
            }
            for (int i = 0; i < 2; i++) { levelDomFromTorporAndTotalRange[i] = levelDomRange[i]; }

            for (int s = 0; s < 8; s++)
            {
                results.Add(new List<double[]>());
                if (activeStats[s])
                {
                    statIOs[s].PostTame = postTamed;
                    double inputValue = statIOs[s].Input / (precisions[s] == 3 ? 100 : 1);
                    double tamingEfficiency = -1, tEUpperBound = (double)this.numericUpDownUpperTEffBound.Value / 100, tELowerBound = (double)this.numericUpDownLowerTEffBound.Value / 100;
                    double vWildL = 0; // value with only wild levels
                    if (checkBoxAlreadyBred.Checked)
                    {
                        // bred creatures always have 100% TE
                        tEUpperBound = 1;
                        tELowerBound = 1;
                    }
                    bool withTEff = (postTamed && stats[c][s][4] > 0);
                    if (withTEff) { statsWithEff.Add(s); }
                    double maxLW = 0;
                    if (stats[c][s][0] > 0 && stats[c][s][1] > 0)
                    {
                        maxLW = Math.Round(((inputValue / (postTamed ? 1 + tELowerBound * stats[c][s][4] : 1) - (postTamed ? stats[c][s][3] : 0)) / stats[c][s][0] - 1) / stats[c][s][1]); // floor is too unprecise
                    }
                    if (s != 7 && maxLW > levelWildFromTorporRange[1]) { maxLW = levelWildFromTorporRange[1]; } // torpor level can be too high right after taming (bug ingame?)

                    double maxLD = 0;
                    if (stats[c][s][0] > 0 && stats[c][s][2] > 0 && postTamed)
                    {
                        maxLD = Math.Round((inputValue / ((stats[c][s][0] + stats[c][s][3]) * (1 + tELowerBound * stats[c][s][4])) - 1) / stats[c][s][2]); //floor is sometimes too unprecise
                    }
                    if (maxLD > levelDomRange[1]) { maxLD = levelDomRange[1]; }

                    for (int w = 0; w < maxLW + 1; w++)
                    {
                        vWildL = stats[c][s][0] + stats[c][s][0] * stats[c][s][1] * w + (postTamed ? stats[c][s][3] : 0);
                        for (int d = 0; d < maxLD + 1; d++)
                        {
                            if (withTEff)
                            {
                                // taming bonus is dependant on taming-efficiency
                                // get tamingEfficiency-possibility
                                // rounding errors need to increase error-range
                                tamingEfficiency = Math.Round((inputValue / (1 + stats[c][s][2] * d) - vWildL) / (vWildL * stats[c][s][4]), 3, MidpointRounding.AwayFromZero);
                                if (tamingEfficiency > 1 && tamingEfficiency < 1.005) { tamingEfficiency = 1; }
                                if (tamingEfficiency >= tELowerBound - 0.005)
                                {
                                    if (tamingEfficiency <= tEUpperBound)
                                    {
                                        results[s].Add(new double[] { w, d, tamingEfficiency });
                                    }
                                    else { continue; }
                                }
                                else
                                {
                                    // if tamingEff < lowerBound, break, as in this loop it's getting only smaller
                                    break;
                                }
                            }
                            else if (Math.Abs((vWildL + vWildL * stats[c][s][2] * d - inputValue) * (precisions[s] == 3 ? 100 : 1)) < 0.2)
                            {
                                results[s].Add(new double[] { w, d, tamingEfficiency });
                                break; // no other solution possible
                            }
                        }
                    }
                }
                else
                {
                    results[s].Add(new double[] { 0, 0, -1 });
                }
            }
            int maxLW2 = levelWildFromTorporRange[1];
            int[] lowerBoundExtraWs = new int[] { 0, 0, 0, 0, 0, 0, 0 };
            int[] lowerBoundExtraDs = new int[] { 0, 0, 0, 0, 0, 0, 0 };
            int[] upperBoundExtraDs = new int[] { 0, 0, 0, 0, 0, 0, 0 };
            // substract all uniquely solved stat-levels
            for (int s = 0; s < 7; s++)
            {
                if (results[s].Count == 1)
                {
                    // result is uniquely solved
                    maxLW2 -= (int)results[s][0][0];
                    levelDomRange[0] -= (int)results[s][0][1];
                    levelDomRange[1] -= (int)results[s][0][1];
                    upperBoundExtraDs[s] = (int)results[s][0][1];
                }
                else if (results[s].Count > 1)
                {
                    // get the smallest and larges value
                    int minW = (int)results[s][0][0], minD = (int)results[s][0][1], maxD = (int)results[s][0][1];
                    for (int r = 1; r < results[s].Count; r++)
                    {
                        if (results[s][r][0] < minW) { minW = (int)results[s][r][0]; }
                        if (results[s][r][1] < minD) { minD = (int)results[s][r][1]; }
                        if (results[s][r][1] > maxD) { maxD = (int)results[s][r][1]; }
                    }
                    // save min/max-possible value
                    lowerBoundExtraWs[s] = minW;
                    lowerBoundExtraDs[s] = minD;
                    upperBoundExtraDs[s] = maxD;
                }
            }
            if (maxLW2 < lowerBoundExtraWs.Sum() || levelDomRange[1] < lowerBoundExtraDs.Sum())
            {
                this.numericUpDownLevel.BackColor = Color.LightSalmon;
                if (!checkBoxAlreadyBred.Checked && this.numericUpDownLowerTEffBound.Value > 0)
                {
                    this.numericUpDownLowerTEffBound.BackColor = Color.LightSalmon;
                }
                if (!checkBoxAlreadyBred.Checked && this.numericUpDownUpperTEffBound.Value < 100)
                {
                    this.numericUpDownUpperTEffBound.BackColor = Color.LightSalmon;
                }
                this.checkBoxAlreadyBred.BackColor = Color.LightSalmon;
                this.checkBoxJustTamed.BackColor = Color.LightSalmon;
                results.Clear();
                resultsValid = false;
            }
            else
            {
                // remove all results that are violate restrictions
                for (int s = 0; s < 7; s++)
                {
                    for (int r = 0; r < results[s].Count; r++)
                    {
                        if (results[s].Count > 1 && (results[s][r][0] > maxLW2 - lowerBoundExtraWs.Sum() + lowerBoundExtraWs[s] || results[s][r][1] > levelDomRange[1] - lowerBoundExtraDs.Sum() + lowerBoundExtraDs[s] || results[s][r][1] < levelDomRange[0] - upperBoundExtraDs.Sum() + upperBoundExtraDs[s]))
                        {
                            results[s].RemoveAt(r--);
                            // if result gets unique due to this, check if remaining result doesn't violate for max level
                            if (results[s].Count == 1)
                            {
                                maxLW2 -= (int)results[s][0][0];
                                levelDomRange[0] -= (int)results[s][0][1];
                                levelDomRange[1] -= (int)results[s][0][1];
                                lowerBoundExtraWs[s] = 0;
                                lowerBoundExtraDs[s] = 0;
                                if (maxLW2 < 0 || levelDomRange[1] < 0)
                                {
                                    this.numericUpDownLevel.BackColor = Color.LightSalmon;
                                    statIOs[s].Status = -2;
                                    statIOs[7].Status = -2;
                                    results[s].Clear();
                                    resultsValid = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                // if more than one parameter is affected by tamingEfficiency filter all numbers that occure only in one
                if (statsWithEff.Count > 1)
                {
                    for (int es = 0; es < statsWithEff.Count; es++)
                    {
                        for (int et = es + 1; et < statsWithEff.Count; et++)
                        {
                            List<int> equalEffs1 = new List<int>();
                            List<int> equalEffs2 = new List<int>();
                            for (int ere = 0; ere < results[statsWithEff[es]].Count; ere++)
                            {
                                for (int erf = 0; erf < results[statsWithEff[et]].Count; erf++)
                                {
                                    // efficiency-calculation can be a bit off due to rounding-ingame, so treat them as equal when diff<0.002
                                    if (Math.Abs(results[statsWithEff[es]][ere][2] - results[statsWithEff[et]][erf][2]) < 0.002)
                                    {
                                        // if entry is not yet in whitelist, add it
                                        if (equalEffs1.IndexOf(ere) == -1) { equalEffs1.Add(ere); }
                                        if (equalEffs2.IndexOf(erf) == -1) { equalEffs2.Add(erf); }
                                    }
                                }
                            }
                            // copy all results that have an efficiency that occurs more than once and replace the others
                            List<double[]> validResults1 = new List<double[]>();
                            for (int ev = 0; ev < equalEffs1.Count; ev++)
                            {
                                validResults1.Add(results[statsWithEff[es]][equalEffs1[ev]]);
                            }
                            // replace long list with (hopefully) shorter list with valid entries
                            results[statsWithEff[es]] = validResults1;
                            List<double[]> validResults2 = new List<double[]>();
                            for (int ev = 0; ev < equalEffs2.Count; ev++)
                            {
                                validResults2.Add(results[statsWithEff[et]][equalEffs2[ev]]);
                            }
                            results[statsWithEff[et]] = validResults2;
                        }
                        if (es >= statsWithEff.Count - 2)
                        {
                            // only one stat left, not enough to compare it
                            break;
                        }
                    }
                }
                for (int s = 0; s < 8; s++)
                {
                    if (results[s].Count > 0)
                    {
                        // display result with most levels in wild, for hp and dm with the most levels in tamed
                        int r = 0;
                        if (s != 0 && s != 5) { r = results[s].Count - 1; }
                        setPossibility(s, r);
                        if (results[s].Count > 1)
                        {
                            statIOs[s].Status = -1;
                        }
                        else { statIOs[s].Status = 1; }
                    }
                    else
                    {
                        statIOs[s].Status = -2;
                        results[s].Clear();
                        resultsValid = false;
                        if (!checkBoxAlreadyBred.Checked && statsWithEff.IndexOf(s) >= 0 && this.numericUpDownLowerTEffBound.Value > 0)
                        {
                            this.numericUpDownLowerTEffBound.BackColor = Color.LightSalmon;
                        }
                        if (!checkBoxAlreadyBred.Checked && statsWithEff.IndexOf(s) >= 0 && this.numericUpDownUpperTEffBound.Value < 100)
                        {
                            this.numericUpDownUpperTEffBound.BackColor = Color.LightSalmon;
                        }
                        this.checkBoxAlreadyBred.BackColor = Color.LightSalmon;
                        this.checkBoxJustTamed.BackColor = Color.LightSalmon;
                    }
                }
            }
            bool speedUnique = false;
            string speedValue = "?";
            if (results.Count == 8 && levelWildFromTorporRange[0] == levelWildFromTorporRange[1])
            {
                speedUnique = true;
                // speed gets remaining wild levels if all other are unique
                int wildSpeedLevel = levelWildFromTorporRange[0];
                for (int s = 0; s < 6; s++)
                {
                    if (results[s].Count != 1)
                    {
                        speedUnique = false;
                        break;
                    }
                    wildSpeedLevel -= (int)results[s][0][0];

                }
                if (speedUnique)
                {
                    speedValue = wildSpeedLevel.ToString();
                }
            }
            statIOs[6].LevelWild = speedValue;
            if (resultsValid)
            {
                buttonCopyClipboard.Enabled = true;
                setActiveStat(activeStatKeeper);
                if (postTamed) { setUniqueTE(); }
                else
                {
                    labelTE.Text = "not yet tamed";
                    labelTE.BackColor = SystemColors.Control;
                }
                showSumOfChosenLevels();
                labelSumWildSB.Text = "≤" + levelWildFromTorporRange[1].ToString();
                labelSumDomSB.Text = (levelDomFromTorporAndTotalRange[0] != levelDomFromTorporAndTotalRange[1] ? levelDomFromTorporAndTotalRange[0].ToString() + "-" : "") + levelDomFromTorporAndTotalRange[1].ToString();
            }
            if (!postTamed)
            {
                labelFootnote.Text = "*Creature is not yet tamed and may get better values then.";
            }
        }

        private void setUniqueTE()
        {
            double eff = uniqueTE();
            if (eff >= 0)
            {
                labelTE.Text = "Extracted: " + Math.Round(100 * eff, 1) + " %";
                labelTE.BackColor = SystemColors.Control;
            }
            else
            {
                labelTE.Text = "TE differs in chosen possibilities";
                labelTE.BackColor = Color.LightSalmon;
            }
        }

        private void statIO_Click(object sender, EventArgs e)
        {
            StatIO se = (StatIO)sender;
            if (se != null)
            {
                setActiveStat(statIOs.IndexOf(se));
            }
        }

        // when clicking on a stat show the possibilites in the listbox
        private void setActiveStat(int stat)
        {
            this.listBoxPossibilities.Items.Clear();
            for (int s = 0; s < 8; s++)
            {
                if (s == stat && statIOs[s].Status == -1)
                {
                    statIOs[s].Selected = true;
                    activeStat = s;
                    setPossibilitiesListbox(s);
                }
                else
                {
                    statIOs[s].Selected = false;
                }
            }
        }

        // fill listbox with possible results of stat
        private void setPossibilitiesListbox(int s)
        {
            if (s < results.Count)
            {
                for (int r = 0; r < results[s].Count; r++)
                {
                    this.listBoxPossibilities.Items.Add(results[s][r][0].ToString() + "\t" + results[s][r][1].ToString() + (results[s][r][2] >= 0 ? "\t" + (results[s][r][2] * 100).ToString() + "%" : ""));
                }
            }
        }

        private void loadFile(bool loadSettings)
        {
            string path = "";
            if (loadSettings)
            {
                // read settings from file
                path = "settings.txt";

                // check if file exists
                if (System.IO.File.Exists(path))
                {
                    string[] rows;
                    rows = System.IO.File.ReadAllLines(path);
                    string[] values;
                    int s = 0;
                    double value = 0;
                    foreach (string row in rows)
                    {
                        if (row.Length > 1 && row.Substring(0, 2) != "//")
                        {
                            values = row.Split(',');
                            if (values.Length == 3)
                            {
                                value = 0;
                                if (Double.TryParse(values[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
                                {
                                    statIOs[s].MultAdd = value;
                                }
                                value = 0;
                                if (Double.TryParse(values[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
                                {
                                    statIOs[s].MultAff = value;
                                }
                                value = 0;
                                if (Double.TryParse(values[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
                                {
                                    statIOs[s].MultLevel = value;
                                }
                                s++;
                            }
                        }
                    }
                }
            }

            // read entities from file
            path = "stats.txt";

            // check if file exists
            if (!System.IO.File.Exists(path))
            {
                MessageBox.Show("Creatures-File '" + path + "' not found.", "Error");
                Close();
            }
            else
            {
                string[] rows;
                rows = System.IO.File.ReadAllLines(path);
                string[] values;
                int c = -1;
                int s = 0;
                comboBoxCreatures.Items.Clear();
                stats.Clear();
                foreach (string row in rows)
                {
                    if (row.Length > 1 && row.Substring(0, 2) != "//")
                    {
                        values = row.Split(',');
                        if (values.Length == 1)
                        {
                            // new creature
                            List<double[]> cs = new List<double[]>();
                            for (s = 0; s < 8; s++)
                            {
                                cs.Add(new double[] { 0, 0, 0, 0, 0 });
                            }
                            s = 0;
                            stats.Add(cs);
                            this.comboBoxCreatures.Items.Add(values[0].Trim());
                            c++;
                        }
                        else if (values.Length > 1 && values.Length < 6)
                        {
                            for (int v = 0; v < values.Length; v++)
                            {
                                if ((s == 5 || s == 6) && v == 0) { stats[c][s][0] = 1; } // damage and speed are handled as percentage of a hidden base value, this tool uses 100% as base, as seen ingame
                                else
                                {
                                    double value = 0;
                                    if (Double.TryParse(values[v], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
                                    {
                                        switch (v)
                                        {
                                            case 2:
                                                value *= statIOs[s].MultLevel;
                                                break;
                                            case 3:
                                                value *= statIOs[s].MultAdd;
                                                break;
                                            case 4:
                                                value *= statIOs[s].MultAff;
                                                break;
                                        }
                                        stats[c][s][v] = value;
                                    }
                                }
                            }
                            s++;
                        }
                    }
                }
            }
        }

        private void comboBoxCreatures_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxCreatures.SelectedIndex >= 0)
            {
                c = comboBoxCreatures.SelectedIndex;
                for (int s = 0; s < 8; s++)
                {
                    activeStats[s] = (stats[c][s][0] > 0);
                    statIOs[s].Enabled = activeStats[s];
                }
                clearAll();
            }
        }

        private void listBoxPossibilities_MouseClick(object sender, MouseEventArgs e)
        {
            int index = this.listBoxPossibilities.IndexFromPoint(e.Location);
            if (index != System.Windows.Forms.ListBox.NoMatches && activeStat >= 0)
            {
                setPossibility(activeStat, index);
            }
        }

        private void setPossibility(int s, int i)
        {
            if (s == 7)
            {
                statIOs[s].LevelWild = "(" + results[s][i][0].ToString() + ")";
            }
            else
            {
                statIOs[s].LevelWild = results[s][i][0].ToString();
                statIOs[s].BarLength = (int)results[s][i][0];
            }
            statIOs[s].LevelDom = results[s][i][1].ToString();
            statIOs[s].BreedingValue = breedingValue(s, i);
            chosenResults[s] = i;
            setUniqueTE();
            showSumOfChosenLevels();
        }

        private void buttonCopyClipboard_Click(object sender, EventArgs e)
        {
            if (results.Count == 8 && chosenResults.Count == 8)
            {
                List<string> tsv = new List<string>();
                int LevelsWildSpeed = (int)results[7][0][0]; // all wild levels, now subtract all the other levels
                for (int s = 0; s < 6; s++) { LevelsWildSpeed -= (int)results[s][chosenResults[s]][0]; }
                string rowLevel = comboBoxCreatures.SelectedItem.ToString() + "\t\t", rowValues = "";
                // if taming efficiency is unique, display it, too
                string effString = "";
                double eff = uniqueTE();
                if (eff >= 0)
                {
                    effString = "\tTamingEff:\t" + (100 * eff).ToString() + "%";
                }
                // headerrow
                if (radioButtonOutputTable.Checked || checkBoxOutputRowHeader.Checked)
                {
                    if (radioButtonOutputTable.Checked)
                    {
                        tsv.Add(comboBoxCreatures.SelectedItem.ToString() + "\tLevel " + numericUpDownLevel.Value.ToString() + effString);
                        tsv.Add("Stat\tWildLevel\tDomLevel\tBreedingValue");
                    }
                    else { tsv.Add("Species\tName\tSex\tHP-Level\tSt-Level\tOx-Level\tFo-Level\tWe-Level\tDm-Level\tSp-Level\tTo-Level\tHP-Value\tSt-Value\tOx-Value\tFo-Value\tWe-Value\tDm-Value\tSp-Value\tTo-Value"); }
                }
                for (int s = 0; s < 8; s++)
                {
                    if (chosenResults[s] < results[s].Count)
                    {
                        string breedingV = "";
                        if (activeStats[s])
                        {
                            if (precisions[s] == 3)
                            {
                                breedingV = (100 * breedingValue(s, chosenResults[s])).ToString() + "%";
                            }
                            else
                            {
                                breedingV = breedingValue(s, chosenResults[s]).ToString();
                            }
                        }
                        if (radioButtonOutputTable.Checked)
                        {
                            tsv.Add(statNames[s] + "\t" + (activeStats[s] ? (s == 6 ? LevelsWildSpeed : results[s][chosenResults[s]][0]).ToString() : "") + "\t" + (activeStats[s] ? results[s][chosenResults[s]][1].ToString() : "") + "\t" + breedingV);
                        }
                        else
                        {
                            rowLevel += "\t" + (activeStats[s] ? (s == 6 ? LevelsWildSpeed : results[s][chosenResults[s]][0]).ToString() : "");
                            rowValues += "\t" + breedingV;
                        }
                    }
                    else { return; }
                }
                if (radioButtonOutputRow.Checked) { tsv.Add(rowLevel + rowValues); }
                Clipboard.SetText(string.Join("\n", tsv));
            }
        }

        private double uniqueTE()
        {
            if (statsWithEff.Count > 0 && results[statsWithEff[0]].Count > chosenResults[statsWithEff[0]])
            {
                double eff = results[statsWithEff[0]][chosenResults[statsWithEff[0]]][2];
                for (int st = 1; st < statsWithEff.Count; st++)
                {
                    // efficiency-calculation can be a bit off due to ingame-rounding
                    if (results[statsWithEff[st]].Count <= chosenResults[statsWithEff[st]] || Math.Abs(results[statsWithEff[st]][chosenResults[statsWithEff[st]]][2] - eff) > 0.002)
                    {
                        return -1;
                    }
                }
                return eff;
            }
            return -1;
        }

        private double breedingValue(int s, int r)
        {
            if (s >= 0 && s < 8)
            {
                if (r >= 0 && r < results[s].Count)
                {
                    return Math.Round((stats[c][s][0] + stats[c][s][0] * stats[c][s][1] * results[s][r][0] + stats[c][s][3]) * (results[s][r][2] >= 0 ? (1 + stats[c][s][4]) : 1), precisions[s], MidpointRounding.AwayFromZero);
                }
            }
            return -1;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/cadon/ARKStatsExtractor");
        }

        private void numericUpDown_Enter(object sender, EventArgs e)
        {
            NumericUpDown n = (NumericUpDown)sender;
            if (n != null)
            {
                n.Select(0, n.Text.Length);
            }
        }

        private void radioButtonOutputRow_CheckedChanged(object sender, EventArgs e)
        {
            this.checkBoxOutputRowHeader.Enabled = radioButtonOutputRow.Checked;
        }

        private void checkBoxAlreadyBred_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxTE.Enabled = !checkBoxAlreadyBred.Checked;
            checkBoxJustTamed.Checked = checkBoxJustTamed.Checked && !checkBoxAlreadyBred.Checked;
        }

        private void checkBoxJustTamed_CheckedChanged(object sender, EventArgs e)
        {
            checkBoxAlreadyBred.Checked = checkBoxAlreadyBred.Checked && !checkBoxJustTamed.Checked;
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            clearAll();
            numericUpDownLevel.Value = 1;
        }

        private void checkBoxSettings_CheckedChanged(object sender, EventArgs e)
        {
            this.SuspendLayout();
            bool t = checkBoxSettings.Checked;
            for (int s = 0; s < 8; s++)
            {
                statIOs[s].Settings = t;
            }
            checkBoxSettings.Text = (t ? "OK" : "Settings");
            if (!t)
            {
                // save settings to file
                string path = "settings.txt";
                string[] content = new string[9];
                content[0] = "// csv of multiplicators: MultAdd,MultAffinity,MultLevel. Order of stats (one per row): Health, Stamina, Oxygen, Food, Weight, Damage, Speed, Torpor";
                for (int s = 0; s < 8; s++)
                {
                    content[s + 1] = statIOs[s].MultAdd.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + statIOs[s].MultAff.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + statIOs[s].MultLevel.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                System.IO.File.WriteAllLines(path, content);

                // update stats according to settings
                loadFile(false);
            }
            this.ResumeLayout();
        }

        private void showSumOfChosenLevels()
        {
            int sumW = 0, sumD = 0;
            bool valid = true, inbound = true;
            for (int s = 0; s < 7; s++)
            {
                if (results[s].Count > chosenResults[s])
                {
                    sumW += (int)results[s][chosenResults[s]][0];
                    sumD += (int)results[s][chosenResults[s]][1];
                }
                else
                {
                    valid = false;
                    break;
                }
            }
            if (valid)
            {
                int speedLvl = 0;
                int.TryParse(statIOs[6].LevelWild, out speedLvl);
                labelSumWild.Text = (sumW + speedLvl).ToString();
                labelSumDom.Text = sumD.ToString();
                if (sumW <= levelWildFromTorporRange[1]) { labelSumWild.ForeColor = SystemColors.ControlText; }
                else
                {
                    labelSumWild.ForeColor = Color.Red;
                    inbound = false;
                }
                if (sumD <= levelDomFromTorporAndTotalRange[1] && sumD >= levelDomFromTorporAndTotalRange[0]) { labelSumDom.ForeColor = SystemColors.ControlText; }
                else
                {
                    labelSumDom.ForeColor = Color.Red;
                    inbound = false;
                }
            }
            else
            {
                labelSumWild.Text = "n/a";
                labelSumDom.Text = "n/a";
            }
            if (inbound)
            {
                panelSums.BackColor = SystemColors.Control;
            }
            else
            {
                panelSums.BackColor = Color.FromArgb(255, 200, 200);
            }

        }
    }
}
