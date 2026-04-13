# ZopfliCompress

使用 [Zopfli](https://github.com/google/zopfli) 对基于 Deflate 算法的文件进行重压缩。\
Uses [Zopfli](https://github.com/google/zopfli) to recompress files based on the Deflate algorithm.

目前，它支持对 PNG 文件中的 [IDAT](https://www.w3.org/TR/png-3/#11IDAT) 数据块进行重压缩。未来将支持对 PNG 文件中的 [iCCP](https://www.w3.org/TR/png-3/#11iCCP)、[zTXt](https://www.w3.org/TR/png-3/#11zTXt)、[iTXt](https://www.w3.org/TR/png-3/#11iTXt) 和 [fdAT](https://www.w3.org/TR/png-3/#fdAT-chunk) 数据块进行重压缩。\
Currently, it supports recompressing [IDAT](https://www.w3.org/TR/png-3/#11IDAT) chunks within PNG files. Future updates will add support for recompressing [iCCP](https://www.w3.org/TR/png-3/#11iCCP), [zTXt](https://www.w3.org/TR/png-3/#11zTXt), [iTXt](https://www.w3.org/TR/png-3/#11iTXt), and [fdAT](https://www.w3.org/TR/png-3/#fdAT-chunk) chunks in PNG files.

此外，还可能支持其他格式，例如 ZIP 等。\
Additionally, support for other formats—such as ZIP archives—may be added in the future.

> [!NOTE]
>
> 本仓库不提供 Zopfli 可执行文件，需要自行获取。\
> This repository does not provide the Zopfli executable; you must obtain it yourself.
