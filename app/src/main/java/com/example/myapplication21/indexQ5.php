
<html>
    <head>
        <meta charset="UTF-8">
        <title>Tutorial 8 Q4</title>
    </head>
    <body>
	    <?php 
			$n = 6;
			$x = 1;
			for ($i=1; $i <= $n-1; $i++) {
				$x *= ($i + 1);
			}
			echo "The factorial of $n = $x" . "\n";
        ?>

    </body>
</html>
