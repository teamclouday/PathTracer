# this script is used to combine color and alpha images

import cv2
import argparse

if __name__=="__main__":
    parse = argparse.ArgumentParser()
    parse.add_argument("-c", "--color", help="color texture map image path", required=True)
    parse.add_argument("-a", "--alpha", help="alpha (opacity) texture map image path", required=True)
    parse.add_argument("-o", "--output", help="output image name", default="output.png")
     
    args = parse.parse_args()
    # read images
    imgColor = cv2.imread(args.color)
    imgAlpha = cv2.imread(args.alpha)
    # convert images
    imgColor = cv2.cvtColor(imgColor, cv2.COLOR_RGB2RGBA)
    imgAlpha = imgAlpha[:, :, 0]
    # add alpha channel
    imgColor[:, :, 3] = imgAlpha
    # save image
    cv2.imwrite(args.output, imgColor)
    print(args.output + " saved")