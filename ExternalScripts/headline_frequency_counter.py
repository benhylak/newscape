import pandas as pd 
import sys
from collections import Counter
import csv 



def process(path):
    ## returns a dict with words as keys and word counts (int) as values

    # imports the data
    data = pd.read_csv(path)
    headlines = data["headline_text"]

    counts = Counter()

    #count the words

    for sentence in headlines:
        counts.update(word.strip('.,?!;"\'').lower() for word in sentence.split())

    return counts

if __name__ == "__main__":
    word_counts = process(sys.argv[1])

    print "Processed {0} words. Exporting to CSV...".format(len(word_counts))

    #output csv file with word counts 
    
    with open('headline_word_counts.csv','wb') as f:
        w = csv.writer(f)
        w.writerows(word_counts.items())
    
    print "Done"
