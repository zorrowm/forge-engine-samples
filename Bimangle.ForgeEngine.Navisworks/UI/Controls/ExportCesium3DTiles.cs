﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Bimangle.ForgeEngine.Common.Formats.Cesium3DTiles;
using Bimangle.ForgeEngine.Common.Formats.Cesium3DTiles.Navisworks;
using Bimangle.ForgeEngine.Common.Georeferenced;
using Bimangle.ForgeEngine.Georeferncing;
using Bimangle.ForgeEngine.Navisworks.Config;
using Bimangle.ForgeEngine.Navisworks.Core;
using Bimangle.ForgeEngine.Navisworks.Helpers;
using Bimangle.ForgeEngine.Navisworks.Utility;

#if EXPRESS
using ExporterX = Bimangle.ForgeEngine.Navisworks.Express.Cesium3DTiles.Exporter;
#else
using ExporterX = Bimangle.ForgeEngine.Navisworks.Pro.Cesium3DTiles.Exporter;
#endif

namespace Bimangle.ForgeEngine.Navisworks.UI.Controls
{
    [Browsable(false)]
    partial class ExportCesium3DTiles : UserControl, IExportControl
    {
        /// <summary>
        /// Draco
        /// </summary>
        private const int GEOMETRY_COMPRESS_TYPE_DEFAULT = 100; 

        private AppConfig _Config;
        private AppConfigCesium3DTiles _LocalConfig;
        private List<FeatureInfo> _Features;

        private List<VisualStyleInfo> _VisualStyles;
        private VisualStyleInfo _VisualStyleDefault;

        private List<ComboItemInfo> _LevelOfDetails;
        private ComboItemInfo _LevelOfDetailDefault;

        private GeoreferncingHost _GeoreferncingHost;

        public TimeSpan ExportDuration { get; private set; }

        public ExportCesium3DTiles()
        {
            InitializeComponent();

            cbGenerateOutline.CheckedChanged += (sender, e) =>
            {
                if (cbGenerateOutline.Checked)
                {
                    cbEnableGeometryCompress.Enabled = false;
                }
                else
                {
                    cbEnableGeometryCompress.Enabled = true;
                }
            };
        }

        #region Overrides of Control

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);

            _GeoreferncingHost?.Dispose();
            _GeoreferncingHost = null;
        }

        #endregion

        string IExportControl.Title => Command.TITLE_3DTILES;

        string IExportControl.Icon => @"3dtiles";

        void IExportControl.Init(AppConfig config)
        {
            _Config = config;
            _LocalConfig = _Config.Cesium3DTiles;

            _GeoreferncingHost = GeoreferncingHost.Create(new GeoreferncingAdapter(_LocalConfig), VersionInfo.GetHomePath());
            _GeoreferncingHost.Preload();

            _Features = new List<FeatureInfo>
            {
                new FeatureInfo(FeatureType.ExcludeTexture, Strings.FeatureNameExcludeTexture, Strings.FeatureDescriptionExcludeTexture, true, false),
                new FeatureInfo(FeatureType.ExcludeLines, Strings.FeatureNameExcludeLines, Strings.FeatureDescriptionExcludeLines),
                new FeatureInfo(FeatureType.ExcludePoints, Strings.FeatureNameExcludePoints, Strings.FeatureDescriptionExcludePoints, true, false),
                new FeatureInfo(FeatureType.OnlySelected, Strings.FeatureNameOnlySelected, Strings.FeatureDescriptionOnlySelected),
                new FeatureInfo(FeatureType.Wireframe, Strings.FeatureNameWireframe, Strings.FeatureDescriptionWireframe, true, false),
                new FeatureInfo(FeatureType.Gray, Strings.FeatureNameGray, Strings.FeatureDescriptionGray, true, false),
                new FeatureInfo(FeatureType.UseBasicRenderColor, string.Empty, string.Empty, true, false),
                new FeatureInfo(FeatureType.GenerateModelsDb, Strings.FeatureNameGenerateModelsDb, Strings.FeatureDescriptionGenerateModelsDb),
                new FeatureInfo(FeatureType.GenerateThumbnail, Strings.FeatureNameGenerateThumbnail, Strings.FeatureDescriptionGenerateThumbnail),
                new FeatureInfo(FeatureType.UseGoogleDraco, Strings.FeatureNameUseGoogleDraco, Strings.FeatureDescriptionUseGoogleDraco, true, false),
                new FeatureInfo(FeatureType.ExtractShell, Strings.FeatureNameExtractShell, Strings.FeatureDescriptionExtractShell, true, false),
                new FeatureInfo(FeatureType.ExportSvfzip, Strings.FeatureNameExportSvfzip, Strings.FeatureDescriptionExportSvfzip, true, false),
                new FeatureInfo(FeatureType.EnableQuantizedAttributes, Strings.FeatureNameEnableQuantizedAttributes, Strings.FeatureDescriptionEnableQuantizedAttributes, true, false),
                new FeatureInfo(FeatureType.EnableTextureWebP, Strings.FeatureNameEnableTextureWebP, Strings.FeatureDescriptionEnableTextureWebP, true, false),
                new FeatureInfo(FeatureType.EnableTextureKtx2, string.Empty, string.Empty, true, false),
                new FeatureInfo(FeatureType.EnableEmbedGeoreferencing, Strings.FeatureNameEnableEmbedGeoreferencing, Strings.FeatureDescriptionEnableEmbedGeoreferencing, true, false),
                new FeatureInfo(FeatureType.EnableUnlitMaterials, Strings.FeatureNameEnableUnlitMaterials, Strings.FeatureDescriptionEnableUnlitMaterials, true, false),
                new FeatureInfo(FeatureType.AutoAlignOriginToSiteCenter, Strings.FeatureNameAutoAlignOriginToSiteCenter, Strings.FeatureDescriptionAutoAlignOriginToSiteCenter, true, false),
                new FeatureInfo(FeatureType.EnableCesiumPrimitiveOutline, Strings.FeatureNameEnableCesiumPrimitiveOutline, Strings.FeatureDescriptionEnableCesiumPrimitiveOutline, true, false),
                new FeatureInfo(FeatureType.EnableMeshOptCompression, string.Empty, string.Empty, true, false),
                new FeatureInfo(FeatureType.EnableMeshQuantized, string.Empty, string.Empty, true, false),
                new FeatureInfo(FeatureType.UseGoogleDracoPatch, string.Empty, string.Empty, true, false),
                new FeatureInfo(FeatureType.ForEarthSdk, string.Empty, Strings.FeatureDescriptionForEarthSdk, true, false),
                new FeatureInfo(FeatureType.Use3DTilesSpecification11, Strings.FeatureNameUse3DTilesSpecification11, Strings.FeatureDescriptionUse3DTilesSpecification11, true, false),
            };

            _VisualStyles = new List<VisualStyleInfo>();
            _VisualStyles.Add(new VisualStyleInfo(@"Colored", Strings.VisualStyleColored + $@"({Strings.TextDefault})", new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, true},
                {FeatureType.Wireframe, false},
                {FeatureType.UseBasicRenderColor, false},
                {FeatureType.Gray, false}
            }));
            _VisualStyles.Add(new VisualStyleInfo(@"Textured", Strings.VisualStyleTextured, new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, false},
                {FeatureType.Wireframe, false},
                {FeatureType.UseBasicRenderColor, true},
                {FeatureType.Gray, false}
            }));
            _VisualStyles.Add(new VisualStyleInfo(@"Realistic", Strings.VisualStyleRealistic, new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, false},
                {FeatureType.Wireframe, false},
                {FeatureType.UseBasicRenderColor, false},
                {FeatureType.Gray, false}
            }));
            _VisualStyleDefault = _VisualStyles.First(x => x.Key == @"Colored");

            _LevelOfDetails = new List<ComboItemInfo>();
            _LevelOfDetails.Add(new ComboItemInfo(-1, Strings.TextAuto));
            for (var i = 0; i <= 8; i++)
            {
                string text;
                switch (i)
                {
                    case 0:
                        text = $@"{i} ({Strings.TextLowest})";
                        break;
                    case 7:
                        text = $@"{i} ({Strings.TextHighest})";
                        break;
                    case 8:
                        text = $@"{i} ({Strings.TextOriginal})";
                        break;
                    default:
                        text = i.ToString();
                        break;
                }

                _LevelOfDetails.Add(new ComboItemInfo(i, text));
            }
            _LevelOfDetailDefault = _LevelOfDetails.Find(x => x.Value == -1);

            cbVisualStyle.Items.Clear();
            cbVisualStyle.Items.AddRange(_VisualStyles.Select(x => (object)x).ToArray());

            cbLevelOfDetail.Items.Clear();
            cbLevelOfDetail.Items.AddRange(_LevelOfDetails.Select(x => (object)x).ToArray());

            cbContentType.Items.Clear();
            cbContentType.Items.Add(new ItemValue<int>(Strings.ContentTypeBasic, 0));
            cbContentType.Items.Add(new ItemValue<int>(Strings.ContentTypeBasicLod, 10));
            cbContentType.Items.Add(new ItemValue<int>(Strings.ContentTypeShellOnlyByElement, 3));
            cbContentType.Items.Add(new ItemValue<int>(Strings.ContentTypeShellOnlyByMesh, 2));

            cbGeometryCompressTypes.Items.Clear();
            cbGeometryCompressTypes.Items.Add(new ItemValue<int>(@"Draco", 100));
            cbGeometryCompressTypes.Items.Add(new ItemValue<int>(@"Mesh Optimizer", 200));
            cbGeometryCompressTypes.Items.Add(new ItemValue<int>(@"Mesh Quantization", 300));
            cbGeometryCompressTypes.Items.Add(new ItemValue<int>(@"Web3D Quantized", 400));
            cbGeometryCompressTypes.Left = cbEnableGeometryCompress.Left + cbEnableGeometryCompress.Width;
            cbGeometryCompressTypes.Enabled = cbEnableGeometryCompress.Checked & cbEnableGeometryCompress.Enabled;

            cbTextureCompressTypes.Items.Clear();
            cbTextureCompressTypes.Items.Add(new ItemValue<int>(@"KTX2 (v1.83+)", 0));
            cbTextureCompressTypes.Items.Add(new ItemValue<int>(@"WebP (v1.54+)", 1));
            cbTextureCompressTypes.Left = cbEnableTextureCompress.Left + cbEnableTextureCompress.Width;
            cbTextureCompressTypes.Enabled = cbEnableTextureCompress.Checked & cbEnableTextureCompress.Enabled;

            cbEnableTextureCompress.CheckedChanged += (sender, e)=>
            {
                cbTextureCompressTypes.Enabled = cbEnableTextureCompress.Checked & cbEnableTextureCompress.Enabled;
            };
            cbEnableTextureCompress.EnabledChanged += (sender, e) =>
            {
                cbTextureCompressTypes.Enabled = cbEnableTextureCompress.Checked & cbEnableTextureCompress.Enabled;
            };

            cbEnableGeometryCompress.CheckedChanged += (sender, e) =>
            {
                cbGeometryCompressTypes.Enabled = cbEnableGeometryCompress.Checked & cbEnableGeometryCompress.Enabled;

                if (cbGeometryCompressTypes.Enabled &&
                    cbGeometryCompressTypes.SelectedItem == null)
                {
                    cbGeometryCompressTypes.SetSelectedValue(GEOMETRY_COMPRESS_TYPE_DEFAULT);
                }
            };
            cbEnableGeometryCompress.EnabledChanged += (sender, e) =>
            {
                cbGeometryCompressTypes.Enabled = cbEnableGeometryCompress.Checked & cbEnableGeometryCompress.Enabled;
            };
        }

        bool IExportControl.Run()
        {
            var filePath = txtTargetPath.Text;
            if (string.IsNullOrEmpty(filePath))
            {
                ShowMessageBox(Strings.MessageSelectOutputPathFirst);
                return false;
            }

            if (Autodesk.Navisworks.Api.Application.ActiveDocument.Models.Count == 0)
            {
                ShowMessageBox(Strings.SceneIsEmpty);
                return false;
            }
            if (File.Exists(filePath) && ShowConfirmBox(Strings.OutputFileExistedWarning) == false)
            {
                return false;
            }

            var homePath = VersionInfo.GetHomePath();
            if (App.CheckHomeFolder(homePath) == false &&
                ShowConfirmBox(Strings.HomeFolderIsInvalid) == false)
            {
                return false;
            }

            //重置 Features 所有特性为 false
            _Features.ForEach(x => x.ChangeSelected(_Features, false));

            var visualStyle = cbVisualStyle.SelectedItem as VisualStyleInfo;
            if (visualStyle != null)
            {
                foreach (var p in visualStyle.Features)
                {
                    _Features.FirstOrDefault(x => x.Type == p.Key)?.ChangeSelected(_Features, p.Value);
                }
            }

            var levelOfDetail = (cbLevelOfDetail.SelectedItem as ComboItemInfo) ?? _LevelOfDetailDefault;

            #region 更新界面选项到 _Features

            void SetFeature(FeatureType featureType, bool selected)
            {
                _Features.FirstOrDefault(x => x.Type == featureType)?.ChangeSelected(_Features, selected);
            }

            //SetFeature(FeatureType.ExportGrids, cbIncludeGrids.Checked);

            SetFeature(FeatureType.ExcludeLines, cbExcludeLines.Checked);
            SetFeature(FeatureType.ExcludePoints, cbExcludeModelPoints.Checked);
            SetFeature(FeatureType.OnlySelected, cbExcludeUnselectedElements.Checked);

            SetFeature(FeatureType.UseGoogleDraco, false);
            SetFeature(FeatureType.EnableQuantizedAttributes, false);
            if (cbEnableGeometryCompress.Checked)
            {
                var geometryGeometryType = cbGeometryCompressTypes.GetSelectedValue<int>();
                switch (geometryGeometryType)
                {
                    case 100:
                        SetFeature(FeatureType.UseGoogleDraco, true);
                        break;
                    case 200:
                        SetFeature(FeatureType.EnableMeshOptCompression, true);
                        break;
                    case 300:
                        SetFeature(FeatureType.EnableMeshQuantized, true);
                        break;
                    case 400:
                        SetFeature(FeatureType.EnableQuantizedAttributes, true);
                        break;
                    default:
                        throw new NotSupportedException($@"GeometryCompressType: {geometryGeometryType}");
                }
            }

            //SetFeature(FeatureType.ExtractShell, cbUseExtractShell.Checked);
            SetFeature(FeatureType.GenerateModelsDb, cbGeneratePropDbSqlite.Checked);
            SetFeature(FeatureType.ExportSvfzip, cbExportSvfzip.Checked);
            //SetFeature(FeatureType.EnableTextureWebP, cbEnableTextureCompress.Checked);
            SetFeature(FeatureType.GenerateThumbnail, cbGenerateThumbnail.Checked);
            SetFeature(FeatureType.EnableCesiumPrimitiveOutline, cbGenerateOutline.Checked);
            SetFeature(FeatureType.EnableUnlitMaterials, cbEnableUnlitMaterials.Checked);
            SetFeature(FeatureType.ForEarthSdk, cbForEarthSdk.Checked);
            SetFeature(FeatureType.Use3DTilesSpecification11, cbUse3DTilesSpecification11.Checked);


            SetFeature(FeatureType.EnableTextureWebP, false);
            SetFeature(FeatureType.EnableTextureKtx2, false);
            if (cbEnableTextureCompress.Checked)
            {
                var textureCompressType = cbTextureCompressTypes.GetSelectedValue<int>() == 1
                    ? FeatureType.EnableTextureWebP
                    : FeatureType.EnableTextureKtx2;
                SetFeature(textureCompressType, true);
            }

            #endregion

            var isCancelled = false;
            using (var session = LicenseConfig.Create())
            {
                if (session.IsValid == false)
                {
                    LicenseConfig.ShowDialog(session, ParentForm);
                    return false;
                }

                #region 保存设置

                var config = _LocalConfig;
                config.Features = _Features.Where(x => x.Selected).Select(x => x.Type).ToList();
                config.LastTargetPath = txtTargetPath.Text;
                config.VisualStyle = visualStyle?.Key;
                config.LevelOfDetail = levelOfDetail?.Value ?? -1;
                config.LevelOfDetailText = levelOfDetail?.ToString();
                config.Mode = cbContentType.GetSelectedValue<int>();

                _Config.Save();

                #endregion

                var sw = Stopwatch.StartNew();
                try
                {
                    var setting = new ExportSetting();
                    setting.LevelOfDetail = config.LevelOfDetail;
                    setting.OutputPath = config.LastTargetPath;
                    setting.Mode = config.Mode;
                    setting.Features = _Features.Where(x => x.Selected && x.Enabled).Select(x => x.Type).ToList();
                    setting.Oem = App.GetOemInfo(homePath);
                    setting.GeoreferencedSetting = _GeoreferncingHost.CreateTargetSetting(config.GeoreferencedSetting);

                    using (var progress = new ProgressExHelper(ParentForm, Strings.MessageExporting))
                    {
                        var cancellationToken = progress.GetCancellationToken();
                        StartExport(setting, progress.GetProgressCallback(), cancellationToken);
                        isCancelled = cancellationToken.IsCancellationRequested;
                    }

                    sw.Stop();
                    var ts = sw.Elapsed;
                    ExportDuration = new TimeSpan(ts.Days, ts.Hours, ts.Minutes, ts.Seconds); //去掉毫秒部分

                    Debug.WriteLine(Strings.MessageOperationSuccessAndElapsedTime, ExportDuration);

                    if (isCancelled == false)
                    {
                        if (config.AutoOpenAllow && config.AutoOpenAppName != null)
                        {
                            Process.Start(config.AutoOpenAppName, config.LastTargetPath);
                        }
                        else
                        {
                            ShowMessageBox(string.Format(Strings.MessageExportSuccess, ExportDuration));
                        }
                    }
                }
                catch (IOException ex)
                {
                    sw.Stop();
                    Debug.WriteLine(Strings.MessageOperationFailureAndElapsedTime, sw.Elapsed);

                    ShowMessageBox(string.Format(Strings.MessageFileSaveFailure, ex.Message));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Debug.WriteLine(Strings.MessageOperationFailureAndElapsedTime, sw.Elapsed);

                    ShowMessageBox(ex.ToString());
                }
            }

            return isCancelled == false;
        }

        void IExportControl.Reset()
        {
            cbVisualStyle.SelectedItem = _VisualStyleDefault;
            cbLevelOfDetail.SelectedItem = _LevelOfDetailDefault;

            cbExcludeLines.Checked = true;
            cbExcludeModelPoints.Checked = true;
            cbExcludeUnselectedElements.Checked = false;

            //cbUseExtractShell.Checked = false;
            cbGeneratePropDbSqlite.Checked = true;
            cbExportSvfzip.Checked = false;
            cbEnableGeometryCompress.Checked = true;
            cbGeometryCompressTypes.SetSelectedValue(GEOMETRY_COMPRESS_TYPE_DEFAULT);    //Default: Draco
            cbEnableTextureCompress.Checked = true;
            cbTextureCompressTypes.SetSelectedValue(0);
            //cbEmbedGeoreferencing.Checked = true;
            cbGenerateThumbnail.Checked = false;
            cbGenerateOutline.Checked = false;
            cbEnableUnlitMaterials.Checked = false;
            cbForEarthSdk.Checked = false;
            cbUse3DTilesSpecification11.Checked = false;

            {
                _LocalConfig.GeoreferencedSetting = _GeoreferncingHost.CreateDefaultSetting();

                txtGeoreferencingInfo.Text = _LocalConfig.GeoreferencedSetting.GetDetails(_GeoreferncingHost);
            }

            cbContentType.SetSelectedValue(0);
        }

        private void FormExport_Load(object sender, EventArgs e)
        {
            if (!DesignMode)
            {
                InitUI();

                txtTargetPath.EnableFolderPathDrop();
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var filePath = txtTargetPath.Text;

            {
                var dialog = this.folderBrowserDialog1;

                if (string.IsNullOrEmpty(filePath) == false)
                {
                    dialog.SelectedPath = filePath;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtTargetPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void cbVisualStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            var visualStyle = cbVisualStyle.SelectedItem as VisualStyleInfo;
            if (visualStyle == null) return;

            foreach (var p in visualStyle.Features)
            {
                _Features.FirstOrDefault(x => x.Type == p.Key)?.ChangeSelected(_Features, p.Value);
            }

            var excludeTexture = _Features.FirstOrDefault(x => x.Type == FeatureType.ExcludeTexture)?.Selected ?? false;
            cbEnableTextureCompress.Enabled = !excludeTexture;
        }

        /// <summary>
        /// 开始导出
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="progressCallback"></param>
        /// <param name="cancellationToken"></param>
        private void StartExport(ExportSetting setting, Action<int> progressCallback, CancellationToken cancellationToken)
        {
            using(var log = new RuntimeLog())
            {
                var exporter = new ExporterX(VersionInfo.GetHomePath());
                exporter.Export(setting, log, progressCallback, cancellationToken);

                //定制化输出成果
                VersionInfo.CustomOutputFor3DTiles(setting.OutputPath);
            }
        }

        private void InitUI()
        {
            var config = _LocalConfig;
            if (config.Features != null && config.Features.Count > 0)
            {
                foreach (var featureType in config.Features)
                {
                    _Features.FirstOrDefault(x=>x.Type == featureType)?.ChangeSelected(_Features, true);
                }
            }

            txtTargetPath.Text = config.LastTargetPath;

            bool IsAllowFeature(FeatureType feature)
            {
                return _Features.Any(x => x.Type == feature && x.Selected);
            }

            #region 基本
            {
                //视觉样式
                var visualStyle = _VisualStyles.FirstOrDefault(x => x.Key == config.VisualStyle) ??
                                  _VisualStyleDefault;
                foreach (var p in visualStyle.Features)
                {
                    _Features.FirstOrDefault(x => x.Type == p.Key)?.ChangeSelected(_Features, p.Value);
                }
                cbVisualStyle.SelectedItem = visualStyle;

                //详细程度
                if (string.IsNullOrEmpty(config.LevelOfDetailText)) config.LevelOfDetail = -1;
                var levelOfDetail = _LevelOfDetails.FirstOrDefault(x => x.Value == config.LevelOfDetail) ??
                                    _LevelOfDetailDefault;
                cbLevelOfDetail.SelectedItem = levelOfDetail;
            }
            #endregion

            #region 排除
            {
                toolTip1.SetToolTip(cbExcludeLines, Strings.FeatureDescriptionExcludeLines);
                toolTip1.SetToolTip(cbExcludeModelPoints, Strings.FeatureDescriptionExcludePoints);
                toolTip1.SetToolTip(cbExcludeUnselectedElements, Strings.FeatureDescriptionOnlySelected);

                if (IsAllowFeature(FeatureType.ExcludeLines))
                {
                    cbExcludeLines.Checked = true;
                }

                if (IsAllowFeature(FeatureType.ExcludePoints))
                {
                    cbExcludeModelPoints.Checked = true;
                }

                if (IsAllowFeature(FeatureType.OnlySelected))
                {
                    cbExcludeUnselectedElements.Checked = true;
                }
            }
            #endregion

            #region 高级
            {
                //toolTip1.SetToolTip(cbUseDraco, Strings.FeatureDescriptionUseGoogleDraco);
                //toolTip1.SetToolTip(cbUseExtractShell, Strings.FeatureDescriptionExtractShell);
                toolTip1.SetToolTip(cbGeneratePropDbSqlite, Strings.FeatureDescriptionGenerateModelsDb);
                toolTip1.SetToolTip(cbExportSvfzip, Strings.FeatureDescriptionExportSvfzip);
                //toolTip1.SetToolTip(cbEnableQuantizedAttributes, Strings.FeatureDescriptionEnableQuantizedAttributes);
                //toolTip1.SetToolTip(cbEnableTextureCompress, Strings.FeatureDescriptionEnableTextureWebP);
                toolTip1.SetToolTip(cbGenerateThumbnail, Strings.FeatureDescriptionGenerateThumbnail);
                toolTip1.SetToolTip(cbGenerateOutline, Strings.FeatureDescriptionEnableCesiumPrimitiveOutline);
                toolTip1.SetToolTip(cbEnableUnlitMaterials, Strings.FeatureDescriptionEnableUnlitMaterials);
                toolTip1.SetToolTip(cbForEarthSdk, Strings.FeatureDescriptionForEarthSdk);
                toolTip1.SetToolTip(cbUse3DTilesSpecification11, Strings.FeatureDescriptionUse3DTilesSpecification11);

                if (IsAllowFeature(FeatureType.UseGoogleDraco))
                {
                    cbGeometryCompressTypes.SetSelectedValue(100);
                    cbEnableGeometryCompress.Checked = true;
                }
                else if (IsAllowFeature(FeatureType.EnableMeshOptCompression))
                {
                    cbGeometryCompressTypes.SetSelectedValue(200);
                    cbEnableGeometryCompress.Checked = true;
                }
                else if (IsAllowFeature(FeatureType.EnableMeshQuantized))
                {
                    cbGeometryCompressTypes.SetSelectedValue(300);
                    cbEnableGeometryCompress.Checked = true;
                }
                else if (IsAllowFeature(FeatureType.EnableQuantizedAttributes))
                {
                    cbGeometryCompressTypes.SetSelectedValue(400);
                    cbEnableGeometryCompress.Checked = true;
                }
                else
                {
                    cbGeometryCompressTypes.SetSelectedValue(GEOMETRY_COMPRESS_TYPE_DEFAULT);
                    cbEnableGeometryCompress.Checked = false;
                }

                //if (IsAllowFeature(FeatureType.ExtractShell))
                //{
                //    cbUseExtractShell.Checked = true;
                //}

                if (IsAllowFeature(FeatureType.GenerateModelsDb))
                {
                    cbGeneratePropDbSqlite.Checked = true;
                }

                if (IsAllowFeature(FeatureType.ExportSvfzip))
                {
                    cbExportSvfzip.Checked = true;
                }

                if (IsAllowFeature(FeatureType.EnableTextureWebP))
                {
                    cbEnableTextureCompress.Checked = true;
                    cbTextureCompressTypes.SetSelectedValue(1);
                }
                else if(IsAllowFeature(FeatureType.EnableTextureKtx2))
                {
                    cbEnableTextureCompress.Checked = true;
                    cbTextureCompressTypes.SetSelectedValue(0);
                }
                else
                {
                    cbEnableTextureCompress.Checked = false;
                    cbTextureCompressTypes.SetSelectedValue(0);
                }

                if (IsAllowFeature(FeatureType.GenerateThumbnail))
                {
                    cbGenerateThumbnail.Checked = true;
                }

                if (IsAllowFeature(FeatureType.EnableCesiumPrimitiveOutline))
                {
                    cbGenerateOutline.Checked = true;
                }

                if (IsAllowFeature(FeatureType.EnableUnlitMaterials))
                {
                    cbEnableUnlitMaterials.Checked = true;
                }

                if (IsAllowFeature(FeatureType.ForEarthSdk))
                {
                    cbForEarthSdk.Checked = true;
                }

                if (IsAllowFeature(FeatureType.Use3DTilesSpecification11))
                {
                    cbUse3DTilesSpecification11.Checked = true;
                }
            }
            #endregion

            #region 3D Tiles

            cbContentType.SetSelectedValue(config.Mode);

            //toolTip1.SetToolTip(cbEmbedGeoreferencing, Strings.FeatureDescriptionEnableEmbedGeoreferencing);

            //cbEmbedGeoreferencing.Checked = IsAllowFeature(FeatureType.EnableEmbedGeoreferencing);

            #endregion

            #region 初始化地理配准信息
            {
                if (config.GeoreferencedSetting == null)
                {
                    config.GeoreferencedSetting = _GeoreferncingHost.CreateDefaultSetting();
                }

                txtGeoreferencingInfo.Text = config.GeoreferencedSetting.GetDetails(_GeoreferncingHost);
            }
            #endregion

#if EXPRESS
            cbExportSvfzip.Enabled = false;
			cbExportSvfzip.Checked = false;
#else
            cbExportSvfzip.Enabled = true;
#endif

        }


        private class VisualStyleInfo
        {
            public string Key { get; }

            private string Text { get; }

            public Dictionary<FeatureType, bool> Features { get; }

            public VisualStyleInfo(string key, string text, Dictionary<FeatureType, bool> features)
            {
                Key = key;
                Text = text;
                Features = features;
            }

            #region Overrides of Object

            public override string ToString()
            {
                return Text;
            }

            #endregion
        }

        private class ComboItemInfo
        {
            public int Value { get; }

            private string Text { get; }

            public ComboItemInfo(int value, string text)
            {
                Value = value;
                Text = text;
            }

            #region Overrides of Object

            public override string ToString()
            {
                return Text;
            }

            #endregion
        }

        private void ShowMessageBox(string message)
        {
            ParentForm.ShowMessageBox(message);
        }

        private bool ShowConfirmBox(string message)
        {
            return MessageBox.Show(ParentForm, message, ParentForm?.Text,
                       MessageBoxButtons.OKCancel,
                       MessageBoxIcon.Question,
                       MessageBoxDefaultButton.Button2) == DialogResult.OK;
        }
        
        private void btnGeoreferncingConfig_Click(object sender, EventArgs e)
        {
            var owner = this.ParentForm;
            var host = _GeoreferncingHost;
            var input = _LocalConfig.GeoreferencedSetting;
            GeoreferncingHelper.ShowGeoreferncingUI(owner, host, input, setting =>
            {
                _LocalConfig.GeoreferencedSetting = setting;

                txtGeoreferencingInfo.Text = _LocalConfig.GeoreferencedSetting.GetDetails(host);
            });
        }
    }
}
