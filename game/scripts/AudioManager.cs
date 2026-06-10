using Fourfall.Core;
using Godot;

namespace Fourfall.Game;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; } = null!;

    private AudioStreamPlayer _tokenLandSoft  = null!;
    private AudioStreamPlayer _tokenLandMetal = null!;
    private AudioStreamPlayer _tokenLandGlass = null!;
    private AudioStreamPlayer _clearWave1     = null!;
    private AudioStreamPlayer _clearWave2     = null!;
    private AudioStreamPlayer _clearWave3     = null!;
    private AudioStreamPlayer _glassShatter   = null!;
    private AudioStreamPlayer _dropEffect     = null!;
    private AudioStreamPlayer _hover          = null!;
    private AudioStreamPlayer _click          = null!;
    private AudioStreamPlayer _rejection      = null!;
    private AudioStreamPlayer _quotaCleared   = null!;
    private AudioStreamPlayer _purchase       = null!;
    private AudioStreamPlayer _purchaseDenied = null!;
    private AudioStreamPlayer _shopClose      = null!;
    private AudioStreamPlayer _sectorAdvance  = null!;
    private AudioStreamPlayer _gameOver       = null!;
    private AudioStreamPlayer _music          = null!;

    public override void _Ready()
    {
        Instance = this;

        var musicStream = GD.Load<AudioStreamMP3>("res://assets/audio/dark-factory-sequence.mp3");
        musicStream.Loop = true;
        _music = new AudioStreamPlayer { Stream = musicStream, VolumeDb = -6f };
        AddChild(_music);
        _music.Play();

        _tokenLandSoft  = Make("res://assets/audio/impactSoft_medium_000.ogg");
        _tokenLandMetal = Make("res://assets/audio/impactMetal_heavy_000.ogg");
        _tokenLandGlass = Make("res://assets/audio/impactGlass_light_000.ogg");
        _clearWave1     = Make("res://assets/audio/powerUp3.ogg");
        _clearWave2     = Make("res://assets/audio/powerUp5.ogg");
        _clearWave3     = Make("res://assets/audio/powerUp8.ogg");
        _glassShatter   = Make("res://assets/audio/impactGlass_heavy_000.ogg");
        _dropEffect     = Make("res://assets/audio/impactMetal_medium_000.ogg");
        _hover          = Make("res://assets/audio/tick_001.ogg", -12f);
        _click          = Make("res://assets/audio/click_001.ogg");
        _rejection      = Make("res://assets/audio/error_001.ogg");
        _quotaCleared   = Make("res://assets/audio/jingles_NES05.ogg");
        _purchase       = Make("res://assets/audio/confirmation_001.ogg");
        _purchaseDenied = Make("res://assets/audio/error_003.ogg");
        _shopClose      = Make("res://assets/audio/close_001.ogg");
        _sectorAdvance  = Make("res://assets/audio/phaseJump1.ogg");
        _gameOver       = Make("res://assets/audio/jingles_HIT02.ogg");
    }

    private AudioStreamPlayer Make(string path, float volumeDb = 0f)
    {
        var player = new AudioStreamPlayer
        {
            Stream = GD.Load<AudioStream>(path),
            VolumeDb = volumeDb,
        };
        AddChild(player);
        return player;
    }

    public void PlayTokenLand(Token token) => (token.Kind switch
    {
        TokenKind.Iron  => _tokenLandMetal,
        TokenKind.Glass => _tokenLandGlass,
        _               => _tokenLandSoft,
    }).Play();

    public void PlayClearWave(int waveNumber) => (waveNumber switch
    {
        1 => _clearWave1,
        2 => _clearWave2,
        _ => _clearWave3,
    }).Play();

    public void PlayGlassShatter()  => _glassShatter.Play();
    public void PlayDropEffect()    => _dropEffect.Play();
    public void PlayHover()         => _hover.Play();
    public void PlayClick()         => _click.Play();
    public void PlayRejection()     => _rejection.Play();
    public void PlayQuotaCleared()  => _quotaCleared.Play();
    public void PlayPurchase()      => _purchase.Play();
    public void PlayPurchaseDenied()=> _purchaseDenied.Play();
    public void PlayShopClose()     => _shopClose.Play();
    public void PlaySectorAdvance() => _sectorAdvance.Play();
    public void PlayGameOver()      => _gameOver.Play();
}
