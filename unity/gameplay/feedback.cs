using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class feedback : MonoBehaviour
{
    public Text half_level;
    public Text Onelife;
    public Text CompensationText;
    public Text Combo_text;
    public Text LastOneText;

    private float TimeToAppear=1.5f;
    private float TimeWhenDisappear;



    public void HalfLevel() // a metà livelo stampa '50%'
    {
        half_level.enabled = true;
        TimeWhenDisappear = Time.time + TimeToAppear;
    }

    public void OneLife()
    {
        Onelife.enabled = true;
        TimeWhenDisappear = Time.time + TimeToAppear;
    }

    public void Combo()
    {
        Combo_text.enabled = true;
        TimeWhenDisappear = Time.time + TimeToAppear;
    }

    public void LastOne()
    {
        LastOneText.enabled = true;
        TimeWhenDisappear = Time.time + TimeToAppear;
    }

    public void Compensation() 
    {
        CompensationText.enabled = true;
        TimeWhenDisappear = Time.time + TimeToAppear;
    }

    private void Update()
    {
        if (Time.time >= TimeWhenDisappear && half_level.enabled == true)   // se nono passati 1.5 secondi da quanto si è attivato, lo disattivo
        {
            half_level.enabled = false;
        }
        if (Time.time >= TimeWhenDisappear && Onelife.enabled == true)
        {
            Onelife.enabled = false;
        }
        if (Time.time >= TimeWhenDisappear && Combo_text.enabled == true)
        {
            Combo_text.enabled = false;
        }
        if (Time.time >= TimeWhenDisappear && LastOneText.enabled == true)
        {
            LastOneText.enabled = false;
        }
        if (Time.time >= TimeWhenDisappear && CompensationText.enabled == true)
        {
            CompensationText.enabled = false;
        }
    }

}
