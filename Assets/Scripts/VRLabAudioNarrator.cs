using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Audio System that synthesizes clean acoustic guidance tones and instructional audio chimes
/// in real time using Unity AudioSource and procedural sound generation (zero external asset dependency).
/// </summary>
public class VRLabAudioNarrator : MonoBehaviour
{
    public static VRLabAudioNarrator Instance { get; private set; }

    private AudioSource audioSource;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.2f; // Slight spatial blend for immersion
        audioSource.volume = 0.85f;
    }

    public void PlayStepChime()
    {
        // 2-tone melodic chime: C5 -> G5
        StartCoroutine(PlayMelodicChime(523.25f, 783.99f, 0.12f));
    }

    public void PlaySuccessFanfare()
    {
        // 3-tone victory chime: E5 -> G5 -> C6
        StartCoroutine(PlayThreeToneChime(659.25f, 783.99f, 1046.50f, 0.15f));
    }

    public void PlayOscillationBeep(int count, int maxCount)
    {
        // Pitch rises subtly with each oscillation cycle
        float freq = 440f + (count * 28f);
        AudioClip clip = GenerateToneClip("OscBeep", freq, 0.08f, 0.4f);
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void PlayWeightSnapSound()
    {
        // Crisp tactile click/snap
        AudioClip clip = GenerateClickClip("SnapClick", 880f, 0.06f);
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void PlayButtonClickSound()
    {
        PlayWeightSnapSound();
    }

    public void PlayWelcomeVoiceover()
    {
        PlayStepChime();
    }

    public void PlayInstructionPromptSound()
    {
        // Gentle attention bell: A5 soft decay
        AudioClip clip = GenerateToneClip("BellPrompt", 880f, 0.35f, 0.5f);
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private IEnumerator PlayMelodicChime(float freq1, float freq2, float noteDuration)
    {
        AudioClip clip1 = GenerateToneClip("Chime1", freq1, noteDuration, 0.6f);
        if (audioSource != null && clip1 != null) audioSource.PlayOneShot(clip1);
        yield return new WaitForSeconds(noteDuration);
        AudioClip clip2 = GenerateToneClip("Chime2", freq2, noteDuration * 1.5f, 0.7f);
        if (audioSource != null && clip2 != null) audioSource.PlayOneShot(clip2);
    }

    private IEnumerator PlayThreeToneChime(float f1, float f2, float f3, float noteDuration)
    {
        AudioClip c1 = GenerateToneClip("Fanfare1", f1, noteDuration, 0.55f);
        if (audioSource != null && c1 != null) audioSource.PlayOneShot(c1);
        yield return new WaitForSeconds(noteDuration);

        AudioClip c2 = GenerateToneClip("Fanfare2", f2, noteDuration, 0.65f);
        if (audioSource != null && c2 != null) audioSource.PlayOneShot(c2);
        yield return new WaitForSeconds(noteDuration);

        AudioClip c3 = GenerateToneClip("Fanfare3", f3, noteDuration * 2.2f, 0.8f);
        if (audioSource != null && c3 != null) audioSource.PlayOneShot(c3);
    }

    private AudioClip GenerateToneClip(string clipName, float frequency, float duration, float peakGain)
    {
        int sampleRate = 44100;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            // Smooth sine wave with smooth attack and exponential decay envelope
            float envelope = Mathf.Sin((float)i / totalSamples * Mathf.PI);
            float wave = Mathf.Sin(2f * Mathf.PI * frequency * t);
            // Slight second harmonic for rich warm chime acoustic
            float waveHarmonic = 0.25f * Mathf.Sin(4f * Mathf.PI * frequency * t);
            samples[i] = (wave + waveHarmonic) * envelope * peakGain;
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip GenerateClickClip(string clipName, float frequency, float duration)
    {
        int sampleRate = 44100;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float decay = Mathf.Exp(-i / (sampleRate * 0.012f));
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * ((float)i / sampleRate));
            samples[i] = (tone * 0.65f + noise) * decay * 0.7f;
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

