using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class tutorialImages : MonoBehaviour {

	int imageNum = 0;
	public GameObject[] images;

	public void nextImage()
{
    changeImage(true);
}

public void start()
	{
		for (int i = 0; i < images.Length; i++)
		{
			if (i == imageNum)
			{
				images[i].SetActive(true);
			}
			else
			{
				images[i].SetActive(false);
			}
		}
	}

public void previousImage()
{
    changeImage(false);
}

public void changeImage(bool next)
	{
		if (imageNum < images.Length && imageNum >= 0)
		{
			if (next)
			{
				imageNum++;
			}
			else
			{
				imageNum--;
			}

			for (int i = 0; i < images.Length; i++)
			{
				if (i == imageNum)
				{
					images[i].SetActive(true);
				}
				else
				{
					images[i].SetActive(false);
				}
			}
		}
		else if (imageNum >= images.Length)
		{
			imageNum = images.Length - 1;
		}
		else if (imageNum < 0)
		{
			imageNum = 0;
		}
		images[imageNum].SetActive(true);
		
	}

}


