using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace NextLogonExec.Scheduling;

public sealed class ComTaskSchedulerClient : ITaskSchedulerClient
{
    private const string TaskSchedulerProgId = "Schedule.Service";
    private const string FolderPath = "\\NextLogonExec";
    private const int TaskTriggerLogon = 9;
    private const int TaskActionExec = 0;
    private const int TaskCreateOrUpdate = 6;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskRunLevelLeastPrivilege = 0;
    private const int TaskRunLevelHighest = 1;
    private const int TaskInstancesIgnoreNew = 2;

    public void RegisterNextLogonTask(ScheduledTaskRegistration registration)
    {
        EnsureWindows();

        try
        {
            dynamic service = CreateService();
            dynamic folder = GetOrCreateFolder(service);
            dynamic definition = service.NewTask(0);

            definition.RegistrationInfo.Description = "Runs a NextLogonExec job once at the next interactive logon.";
            definition.Principal.UserId = registration.UserId;
            definition.Principal.LogonType = TaskLogonInteractiveToken;
            definition.Principal.RunLevel = registration.Elevated ? TaskRunLevelHighest : TaskRunLevelLeastPrivilege;

            dynamic trigger = definition.Triggers.Create(TaskTriggerLogon);
            trigger.UserId = registration.UserId;
            if (registration.DelaySeconds > 0)
            {
                trigger.Delay = "PT" + registration.DelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "S";
            }

            dynamic action = definition.Actions.Create(TaskActionExec);
            action.Path = registration.ActionPath;
            action.Arguments = registration.ActionArguments;

            definition.Settings.Enabled = true;
            definition.Settings.Hidden = true;
            definition.Settings.StartWhenAvailable = true;
            definition.Settings.MultipleInstances = TaskInstancesIgnoreNew;
            definition.Settings.ExecutionTimeLimit = "PT0S";

            folder.RegisterTaskDefinition(
                registration.Id,
                definition,
                TaskCreateOrUpdate,
                null,
                null,
                TaskLogonInteractiveToken,
                null);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or RuntimeBinderException)
        {
            throw new TaskSchedulerClientException($"Failed to register scheduled task '{registration.Id}'.", ex);
        }
    }

    public void UnregisterTask(string id)
    {
        EnsureWindows();

        try
        {
            dynamic service = CreateService();
            dynamic folder = GetFolder(service);
            folder.DeleteTask(id, 0);
        }
        catch (COMException ex) when (IsNotFound(ex))
        {
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or RuntimeBinderException)
        {
            throw new TaskSchedulerClientException($"Failed to unregister scheduled task '{id}'.", ex);
        }
    }

    public bool TaskExists(string id)
    {
        EnsureWindows();

        try
        {
            dynamic service = CreateService();
            dynamic folder = GetFolder(service);
            _ = folder.GetTask(id);
            return true;
        }
        catch (COMException ex) when (IsNotFound(ex))
        {
            return false;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or RuntimeBinderException)
        {
            throw new TaskSchedulerClientException($"Failed to query scheduled task '{id}'.", ex);
        }
    }

    private static dynamic CreateService()
    {
        // Task Scheduler 2.0 には .NET 向けの標準参照アセンブリがないため、
        // COM の生成と dynamic 呼び出しをこの境界だけに閉じ込める。
        Type serviceType = Type.GetTypeFromProgID(TaskSchedulerProgId)
            ?? throw new TaskSchedulerClientException("Task Scheduler COM service is not registered.");
        dynamic service = Activator.CreateInstance(serviceType)
            ?? throw new TaskSchedulerClientException("Failed to create Task Scheduler COM service.");
        service.Connect();
        return service;
    }

    private static dynamic GetOrCreateFolder(dynamic service)
    {
        dynamic root = service.GetFolder("\\");
        try
        {
            return GetFolder(service);
        }
        catch (COMException ex) when (IsNotFound(ex))
        {
            return root.CreateFolder("NextLogonExec", null);
        }
    }

    private static dynamic GetFolder(dynamic service)
    {
        return service.GetFolder(FolderPath);
    }

    private static bool IsNotFound(COMException ex)
    {
        return unchecked((uint)ex.HResult) is 0x80070002 or 0x8004130F;
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new UnsupportedPlatformException("Task Scheduler is only available on Windows.");
        }
    }
}
