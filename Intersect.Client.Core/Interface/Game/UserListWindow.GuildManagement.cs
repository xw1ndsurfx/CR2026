using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control.EventArguments.InputSubmissionEvent;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Network.Packets.Server;
namespace Intersect.Client.Interface.Game;
internal sealed partial class UserListWindow
{
    private bool _guildManagementOpen;
    private Guid _managedMember;
    private int _managedRank;
    private static string GuildText(string text)=>Intersect.Framework.Core.QualityOfLife.QualityOfLifeText.Get(ClientLanguages.Text(text),ClientLanguages.Current);
    private static bool CanManageGuild=>Globals.Me is {} p && (p.Rank==0 || p.GuildRank?.Permissions is {} permissions && (permissions.Promote || permissions.Demote || permissions.Kick));
    private void DrawGuildManagement()
    {
        var p=Globals.Me;if(p==null)return;
        AddLabel(_body,"GuildManagementTitle",GuildText("Guild management"),12,10,442,35,18,Gold,true);
        AddLabel(_body,"GuildManagementHint",GuildText("Select a member and the rank to assign."),12,53,442,28,11,Cream);
        var members=(p.GuildMembers??[]).Where(m=>m.Id!=p.Id).OrderBy(m=>m.Name).ToArray();
        var memberChoice=new ComboBox(_body,"GuildManagedMember"){Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11};memberChoice.SetBounds(12,91,442,32);
        foreach(var member in members)memberChoice.AddItem(member.Name,userData:member.Id);
        if(!memberChoice.SelectByUserData(_managedMember) && members.FirstOrDefault() is {} first)memberChoice.SelectByUserData(first.Id);
        var currentRank=AddLabel(_body,"GuildCurrentRank","",12,133,442,27,11,Cream);
        var rankChoice=new ComboBox(_body,"GuildAssignedRank"){Font=GameContentManager.Current.GetFont("sourcesanspro")??Skin.DefaultFont,FontSize=11};rankChoice.SetBounds(12,174,442,32);
        for(var i=p.Rank+1;i<Options.Instance.Guild.Ranks.Length;i++)rankChoice.AddItem(Options.Instance.Guild.Ranks[i].Title,userData:i);
        if(!rankChoice.SelectByUserData(_managedRank))rankChoice.SelectByUserData(p.Rank+1);
        var promote=AddAction(_body,"GuildPromote",GuildText("Promote"),12,229,214,36,()=>ConfirmGuildManagement("promote"));
        var demote=AddAction(_body,"GuildDemote",GuildText("Demote"),240,229,214,36,()=>ConfirmGuildManagement("demote"));
        var kick=AddAction(_body,"GuildKick",GuildText("Remove member"),12,281,214,36,()=>ConfirmGuildManagement("kick"));
        var transfer=AddAction(_body,"GuildTransfer",GuildText("Transfer leadership"),240,281,214,36,()=>ConfirmGuildManagement("transfer"));
        void UpdateActions()
        {
            _managedMember=memberChoice.SelectedItem?.UserData as Guid? ?? Guid.Empty;
            _managedRank=rankChoice.SelectedItem?.UserData as int? ?? -1;
            var member=members.FirstOrDefault(m=>m.Id==_managedMember);
            currentRank.Text=string.Format(GuildText("Current rank: {0}"),member is {} m && m.Rank>=0 && m.Rank<Options.Instance.Guild.Ranks.Length?Options.Instance.Guild.Ranks[m.Rank].Title:"-");
            promote.IsDisabled=!GuildManagementAllowed("promote");demote.IsDisabled=!GuildManagementAllowed("demote");
            kick.IsDisabled=!GuildManagementAllowed("kick");transfer.IsDisabled=!GuildManagementAllowed("transfer");
        }
        memberChoice.ItemSelected+=(_,_)=>UpdateActions();rankChoice.ItemSelected+=(_,_)=>UpdateActions();UpdateActions();
        AddAction(_footer,"GuildMembersBack",GuildText("Back to members"),6,13,230,42,()=>{_guildManagementOpen=false;Refresh();});
        var canInvite=p.Rank==0 || p.GuildRank?.Permissions.Invite==true;
        AddAction(_footer,"GuildInvite",GuildText("Invite"),254,13,170,42,canInvite?InviteGuildMember:null);
        _body.SetInnerSize(468,426);
    }
    private bool GuildManagementAllowed(string action)
    {
        var p=Globals.Me;var member=p?.GuildMembers?.FirstOrDefault(m=>m.Id==_managedMember);
        if(p==null || member==null || member.Id==p.Id || member.Rank<=p.Rank)return false;
        var owner=p.Rank==0;var permissions=p.GuildRank?.Permissions;
        if(action=="kick")return owner || permissions?.Kick==true;
        if(action=="transfer")return owner;
        if(_managedRank<=p.Rank || _managedRank>=Options.Instance.Guild.Ranks.Length)return false;
        return action=="promote" ? (owner || permissions?.Promote==true) && _managedRank<member.Rank
            : (owner || permissions?.Demote==true) && _managedRank>member.Rank;
    }
    private void ConfirmGuildManagement(string action)
    {
        if(!GuildManagementAllowed(action))return;
        var member=Globals.Me!.GuildMembers.First(m=>m.Id==_managedMember);var rank=_managedRank;
        if(action=="transfer")
        {
            _=new InputBox(Strings.Guilds.TransferTitle,Strings.Guilds.TransferToMemberPrompt.ToString(member.Name,Globals.Me.GuildRank!.Title,Globals.Me.Guild),InputType.TextInput,
                onSubmit:(_,args)=>{if(args.Value is StringSubmissionValue value && value.Value?.Trim()==Globals.Me?.Guild && GuildManagementAllowed(action))PacketSender.SendTransferGuild(member.Id);});return;
        }
        var title=action=="promote"?Strings.Guilds.PromoteTitle:action=="demote"?Strings.Guilds.DemoteTitle:Strings.Guilds.KickTitle;
        var prompt=action=="promote"?Strings.Guilds.PromotePrompt.ToString(member.Name,Options.Instance.Guild.Ranks[rank].Title)
            :action=="demote"?Strings.Guilds.DemotePrompt.ToString(member.Name,Options.Instance.Guild.Ranks[rank].Title):Strings.Guilds.KickPrompt.ToString(member.Name);
        _=new InputBox(title,prompt,InputType.YesNo,onSubmit:(_,_)=>
        {
            // Server permissions are checked again when this request is handled.
            if(action=="promote")PacketSender.SendPromoteGuildMember(member.Id,rank);
            else if(action=="demote")PacketSender.SendDemoteGuildMember(member.Id,rank);
            else PacketSender.SendKickGuildMember(member.Id);
        });
    }
}
