using System.Drawing;
using DarkUI.Controls;
using Intersect.Editor.Forms.Helpers;
using Intersect.Editor.Localization;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Animations;
using Intersect.Framework.Core.GameObjects.Events;
using Intersect.Framework.Core.GameObjects.Events.Commands;
using Intersect.Framework.Core.GameObjects.Maps;
using Intersect.Framework.Core.GameObjects.Maps.MapList;
using Intersect.GameObjects;

namespace Intersect.Editor.Forms.Editors.Events.Event_Commands;


public partial class EventCommandPlayAnimation : UserControl
{

    private readonly FrmEvent mEventEditor;

    private MapDescriptor mCurrentMap;

    private EventDescriptor mEditingEvent;

    private PlayAnimationCommand mMyCommand;

    private int mSpawnX;

    private int mSpawnY;

    private Grid? mGrid;

    private DarkNumericUpDown mOffsetX = null!;

    private DarkNumericUpDown mOffsetY = null!;

    private Label mOffsetXLabel = null!;

    private Label mOffsetYLabel = null!;

    public EventCommandPlayAnimation(
        FrmEvent eventEditor,
        MapDescriptor currentMap,
        EventDescriptor currentEvent,
        PlayAnimationCommand editingCommand
    )
    {
        InitializeComponent();
        InitializePixelOffsetControls();
        mMyCommand = editingCommand;
        mEventEditor = eventEditor;
        mEditingEvent = currentEvent;
        mCurrentMap = currentMap;
        InitLocalization();
        cmbAnimation.Items.Clear();
        cmbAnimation.Items.AddRange(AnimationDescriptor.Names);
        cmbAnimation.SelectedIndex = AnimationDescriptor.ListIndex(mMyCommand.AnimationId);
        if (mMyCommand.MapId != Guid.Empty)
        {
            cmbConditionType.SelectedIndex = 0;
        }
        else
        {
            cmbConditionType.SelectedIndex = 1;
        }

        chkInstanceToPlayer.Checked = mMyCommand.InstanceToPlayer;
        mOffsetX.Value = Math.Clamp(mMyCommand.OffsetX, (int)mOffsetX.Minimum, (int)mOffsetX.Maximum);
        mOffsetY.Value = Math.Clamp(mMyCommand.OffsetY, (int)mOffsetY.Minimum, (int)mOffsetY.Maximum);

        nudWarpX.Maximum = Options.Instance.Map.MapWidth;
        nudWarpY.Maximum = Options.Instance.Map.MapHeight;
        UpdateFormElements();
        switch (cmbConditionType.SelectedIndex)
        {
            case 0: //Tile spawn
                //Fill in the map cmb
                nudWarpX.Value = mMyCommand.X;
                nudWarpY.Value = mMyCommand.Y;
                cmbDirection.SelectedIndex = mMyCommand.Dir;

                break;
            case 1: //On/Around Entity Spawn
                mSpawnX = mMyCommand.X;
                mSpawnY = mMyCommand.Y;
                switch (mMyCommand.Dir)
                {
                    //0 does not adhere to direction, 1 is Spawning Relative to Direction, 2 is Rotating Relative to Direction, and 3 is both.
                    case 1:
                        chkRelativeLocation.Checked = true;

                        break;
                    case 2:
                        chkRotateDirection.Checked = true;

                        break;
                    case 3:
                        chkRelativeLocation.Checked = true;
                        chkRotateDirection.Checked = true;

                        break;
                }

                UpdateSpawnPreview();

                break;
        }
    }

    private void InitializePixelOffsetControls()
    {
        // Leave the existing 5x5 tile selector intact and add fine pixel positioning
        // underneath it for animations that are attached directly to an entity/event.
        grpEntitySpawn.Size = new Size(275, 388);
        grpPlayAnimation.Size = new Size(299, 552);
        chkRelativeLocation.Location = new System.Drawing.Point(44, 332);
        chkRotateDirection.Location = new System.Drawing.Point(44, 358);
        chkInstanceToPlayer.Location = new System.Drawing.Point(10, 487);
        btnSave.Location = new System.Drawing.Point(103, 515);
        btnCancel.Location = new System.Drawing.Point(197, 515);
        Size = new Size(312, 560);

        mOffsetXLabel = new Label
        {
            AutoSize = true,
            Location = new System.Drawing.Point(44, 276),
            Text = "Offset X (px):",
        };

        mOffsetX = new DarkNumericUpDown
        {
            Location = new System.Drawing.Point(145, 272),
            Minimum = -1024,
            Maximum = 1024,
            Size = new Size(86, 23),
        };

        mOffsetYLabel = new Label
        {
            AutoSize = true,
            Location = new System.Drawing.Point(44, 305),
            Text = "Offset Y (px):",
        };

        mOffsetY = new DarkNumericUpDown
        {
            Location = new System.Drawing.Point(145, 301),
            Minimum = -1024,
            Maximum = 1024,
            Size = new Size(86, 23),
        };

        grpEntitySpawn.Controls.Add(mOffsetXLabel);
        grpEntitySpawn.Controls.Add(mOffsetX);
        grpEntitySpawn.Controls.Add(mOffsetYLabel);
        grpEntitySpawn.Controls.Add(mOffsetY);
    }

    private void InitLocalization()
    {
        grpPlayAnimation.Text = Strings.EventPlayAnimation.title;
        lblAnimation.Text = Strings.EventPlayAnimation.animation;
        lblSpawnType.Text = Strings.EventPlayAnimation.spawntype;
        cmbConditionType.Items.Clear();
        cmbConditionType.Items.Add(Strings.EventPlayAnimation.spawntype0);
        cmbConditionType.Items.Add(Strings.EventPlayAnimation.spawntype1);

        grpTileSpawn.Text = Strings.EventPlayAnimation.spawntype0;
        grpEntitySpawn.Text = Strings.EventPlayAnimation.spawntype1;

        lblMap.Text = Strings.Warping.map.ToString("");
        lblX.Text = Strings.Warping.x.ToString("");
        lblY.Text = Strings.Warping.y.ToString("");
        lblDir.Text = Strings.Warping.direction.ToString("");
        cmbDirection.Items.Clear();
        for (var i = 0; i < 4; i++)
        {
            cmbDirection.Items.Add(Strings.Direction.dir[(Direction)i]);
        }

        cmbDirection.SelectedIndex = 0;

        lblEntity.Text = Strings.EventPlayAnimation.entity;
        lblRelativeLocation.Text = Strings.EventPlayAnimation.relativelocation;
        chkRelativeLocation.Text = Strings.EventPlayAnimation.spawnrelative;
        chkRotateDirection.Text = Strings.EventPlayAnimation.rotaterelative;

        chkInstanceToPlayer.Text = Strings.EventPlayAnimation.InstanceToPlayer;
        
        ToolTip instanceTooltip = new ToolTip()
        {
            InitialDelay = 1000,
            ReshowDelay = 500
        };
        instanceTooltip.SetToolTip(chkInstanceToPlayer, Strings.EventPlayAnimation.InstanceToPlayerTooltip);

        btnSave.Text = Strings.EventPlayAnimation.okay;
        btnCancel.Text = Strings.EventPlayAnimation.cancel;
    }

    private void UpdateFormElements()
    {
        grpTileSpawn.Hide();
        grpEntitySpawn.Hide();
        switch (cmbConditionType.SelectedIndex)
        {
            case 0: //Tile Spawn
                grpTileSpawn.Show();
                cmbMap.Items.Clear();
                for (var i = 0; i < MapList.OrderedMaps.Count; i++)
                {
                    cmbMap.Items.Add($@"{MapList.OrderedMaps[i].Name} ({MapList.OrderedMaps[i].MapId})");
                    if (MapList.OrderedMaps[i].MapId == mMyCommand.MapId)
                    {
                        cmbMap.SelectedIndex = i;
                    }
                }

                if (cmbMap.SelectedIndex == -1)
                {
                    cmbMap.SelectedIndex = 0;
                }

                break;
            case 1: //On/Around Entity Spawn
                grpEntitySpawn.Show();
                cmbEntities.Items.Clear();
                cmbEntities.Items.Add(Strings.EventPlayAnimation.player);
                cmbEntities.SelectedIndex = 0;

                if (!mEditingEvent.CommonEvent)
                {
                    foreach (var evt in mCurrentMap.LocalEvents)
                    {
                        cmbEntities.Items.Add(
                            evt.Key == mEditingEvent.Id
                                ? Strings.EventPlayAnimation.This + " "
                                : "" + evt.Value.Name
                        );

                        if (mMyCommand.EntityId == evt.Key)
                        {
                            cmbEntities.SelectedIndex = cmbEntities.Items.Count - 1;
                        }
                    }
                }

                UpdateSpawnPreview();

                break;
        }
    }

    private void UpdateSpawnPreview()
    {
        if (mGrid == null)
        {
            mGrid = new Grid
            {
                DisplayWidth = pnlSpawnLoc.Width,
                DisplayHeight = pnlSpawnLoc.Height,
                Columns = 5,
                Rows = 5,
                Cells = new[] { new GridCell(2, 2, null, "E") }
            };
        }

        pnlSpawnLoc.BackgroundImage = GridHelper.DrawGrid(
            mGrid.Value.WithAdditionalCells(
                new GridCell(mSpawnX + 2, mSpawnY + 2, System.Drawing.Color.Red)
            )
        );
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        mMyCommand.AnimationId = AnimationDescriptor.IdFromList(cmbAnimation.SelectedIndex);
        switch (cmbConditionType.SelectedIndex)
        {
            case 0: //Tile Spawn
                mMyCommand.EntityId = Guid.Empty;
                mMyCommand.MapId = MapList.OrderedMaps[cmbMap.SelectedIndex].MapId;
                mMyCommand.X = (sbyte) nudWarpX.Value;
                mMyCommand.Y = (sbyte) nudWarpY.Value;
                mMyCommand.Dir = (byte) cmbDirection.SelectedIndex;
                mMyCommand.OffsetX = 0;
                mMyCommand.OffsetY = 0;

                break;
            case 1: //On/Around Entity Spawn
                mMyCommand.MapId = Guid.Empty;
                if (cmbEntities.SelectedIndex == 0 || cmbEntities.SelectedIndex == -1)
                {
                    mMyCommand.EntityId = Guid.Empty;
                }
                else
                {
                    mMyCommand.EntityId = mCurrentMap.LocalEvents.Keys.ToList()[cmbEntities.SelectedIndex - 1];
                }

                mMyCommand.X = (sbyte) mSpawnX;
                mMyCommand.Y = (sbyte) mSpawnY;
                if (chkRelativeLocation.Checked && chkRotateDirection.Checked)
                {
                    mMyCommand.Dir = 3;

                    //0 does not adhere to direction, 1 is Spawning Relative to Direction, 2 is Rotating Relative to Direction, and 3 is both.
                }
                else if (chkRelativeLocation.Checked)
                {
                    mMyCommand.Dir = 1;

                    //0 does not adhere to direction, 1 is Spawning Relative to Direction, 2 is Rotating Relative to Direction, and 3 is both.
                }
                else if (chkRotateDirection.Checked)
                {
                    mMyCommand.Dir = 2;

                    //0 does not adhere to direction, 1 is Spawning Relative to Direction, 2 is Rotating Relative to Direction, and 3 is both.
                }
                else
                {
                    mMyCommand.Dir = 0;

                    //0 does not adhere to direction, 1 is Spawning Relative to Direction, 2 is Rotating Relative to Direction, and 3 is both.
                }

                mMyCommand.OffsetX = (int)mOffsetX.Value;
                mMyCommand.OffsetY = (int)mOffsetY.Value;

                break;
        }

        mMyCommand.InstanceToPlayer = chkInstanceToPlayer.Checked;

        mEventEditor.FinishCommandEdit();
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        mEventEditor.CancelCommandEdit();
    }

    private void cmbConditionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        UpdateFormElements();
    }

    private void btnVisual_Click(object sender, EventArgs e)
    {
        var frmWarpSelection = new FrmWarpSelection();
        frmWarpSelection.SelectTile(
            MapList.OrderedMaps[cmbMap.SelectedIndex].MapId, (int) nudWarpX.Value, (int) nudWarpY.Value
        );

        frmWarpSelection.ShowDialog();
        if (frmWarpSelection.GetResult())
        {
            for (var i = 0; i < MapList.OrderedMaps.Count; i++)
            {
                if (MapList.OrderedMaps[i].MapId == frmWarpSelection.GetMap())
                {
                    cmbMap.SelectedIndex = i;

                    break;
                }
            }

            nudWarpX.Value = frmWarpSelection.GetX();
            nudWarpY.Value = frmWarpSelection.GetY();
        }
    }

    private void pnlSpawnLoc_MouseDown(object sender, MouseEventArgs e)
    {
        if (mGrid == null)
        {
            return;
        }

        var cell = GridHelper.CellFromPoint(mGrid.Value, e.X, e.Y);
        if (cell != null)
        {
            (mSpawnX, mSpawnY) = cell.Value;
            UpdateSpawnPreview();
        }
    }

}
