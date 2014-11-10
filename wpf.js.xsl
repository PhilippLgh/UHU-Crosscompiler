<?xml version="1.0" encoding="UTF-8"?>

<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  
<!-- enclose the output with a valid html skeleton -->  
<xsl:template match="/">
    <html lang="en">
      <head>
        <meta charset="UTF-8"/>
        <title>Auto Generated WPF.JS App</title>
        <link rel="import" href="./WPF.JS/wpf.js.import.html" />
      </head>
      <body>
        <xsl:apply-templates select="node()|@*"/>
      </body>
    </html>
</xsl:template>

 <xsl:template match="node()|@*">
  <xsl:copy>
   <xsl:apply-templates select="node()|@*"/>
  </xsl:copy>
 </xsl:template>

  <!-- prefix every element in the document with WPF- and continue recursively -->
  <xsl:template match="*">
    <xsl:element name="WPF-{name()}">
      <xsl:apply-templates select="node()|@*"/>
    </xsl:element>
  </xsl:template>


</xsl:stylesheet>

