# this script converts an alpha image to a color map that can be used in cutoff mode

import cv2
import argparse
import numpy as np

if __name__=="__main__":
    parse = argparse.ArgumentParser()
    parse.add_argument("-a", "--alpha", help="alpha texture map image path", required=True)
    parse.add_argument("-o", "--output", help="output image name", default="output.png")
     
    args = parse.parse_args()
    # read images
    img = cv2.imread(args.alpha)
    # convert images
    imgAlpha = img[:, :, 0]
    # add alpha channel
    img = cv2.cvtColor(img, cv2.COLOR_RGB2RGBA)
    img[:, :, 3] = imgAlpha
    # save image
    cv2.imwrite(args.output, img)
    print(args.output + " saved")