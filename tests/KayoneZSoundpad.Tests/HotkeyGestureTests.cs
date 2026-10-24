using System.IO;
using Xunit;
using KayoneZSoundpad.Audio;
using KayoneZSoundpad.Hotkeys;
using KayoneZSoundpad.Models;
using KayoneZSoundpad.Services;

namespace KayoneZSoundpad.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void ToString_Formats_Modifiers()
    {
        var g = new HotkeyGesture { VirtualKey = 'K', Modifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt };
        Assert.Equal("Ctrl + Alt + K", g.ToString());
    }

    [Fact]
    public void ToString_FunctionKeys()
    {
        var g = new HotkeyGesture { VirtualKey = 0x70, Modifiers = HotkeyModifiers.None }; // F1
        Assert.Equal("F1", g.ToString());
        var g2 = new HotkeyGesture { VirtualKey = 0x74, Modifiers = HotkeyModifiers.None }; // F5
        Assert.Equal("F5", g2.ToString());
    }

    [Fact]
    public void Equality_ByVkAndModifiers()
    {
        var a = new HotkeyGesture { VirtualKey = 0x43, Modifiers = HotkeyModifiers.Ctrl };
        var b = new HotkeyGesture { VirtualKey = 0x43, Modifiers = HotkeyModifiers.Ctrl };
        var c = new HotkeyGesture { VirtualKey = 0x43, Modifiers = HotkeyModifiers.None };
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Invalid_Gesture_WhenVkZero()
    {
        Assert.False(new HotkeyGesture().IsValid);
        Assert.True(new HotkeyGesture { VirtualKey = 0x20 }.IsValid);
    }

    [Fact]
    public void HotkeyService_Register_FindOwner_Unregister()
    {
        using var svc = new GlobalHotkeyService();
        var id1 = Guid.NewGuid();
        var gesture = new HotkeyGesture { VirtualKey = 0x78, Modifiers = HotkeyModifiers.Ctrl }; // Ctrl+F9

        svc.Register(gesture, id1);
        Assert.Equal(id1, svc.FindOwner(gesture));
        Assert.Null(svc.FindOwner(new HotkeyGesture { VirtualKey = 0x79 }));

        svc.UnregisterAction(id1);
        Assert.Null(svc.FindOwner(gesture));
    }

    [Fact]
    public void HotkeyService_SameGesture_TakesLatestRegistration()
    {
        using var svc = new GlobalHotkeyService();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var gesture = new HotkeyGesture { VirtualKey = 0x79 };

        svc.Register(gesture, id1);
        svc.Register(gesture, id2);

        Assert.Equal(id2, svc.FindOwner(gesture));
    }
}


