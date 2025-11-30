Map
In the domain map coordinates X and Y started from 0 at the bottom-left corner.
MapLayoutResponse returns coordinates where Y starts from 0 at the top-left corner. 
For calculating console map coordinates Y should be reverted.
Example for 5x5 map:
```
Domain → Console (5×5)

X0Y0 → X0Y4 X1Y0 → X1Y4 X2Y0 → X2Y4
X0Y1 → X0Y3 X1Y1 → X1Y3 X2Y1 → X2Y3
X0Y2 → X0Y2 X1Y2 → X1Y2 X2Y2 → X2Y2
X0Y3 → X0Y1 X1Y3 → X1Y1 X2Y3 → X2Y1
X0Y4 → X0Y0 X1Y4 → X1Y0 X2Y4 → X2Y0
```
